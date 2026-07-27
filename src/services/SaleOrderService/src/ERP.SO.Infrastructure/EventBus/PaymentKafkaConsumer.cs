using Confluent.Kafka;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using ERP.SO.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.SO.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes PaymentService's "payment-topic" (Outbox pattern, Newtonsoft
    /// TypeNameHandling.All — dispatch by "$type" substring, mirrors
    /// ProductPriceChangedConsumer). Feeds OrderFulfillmentSaga's
    /// HandlePaymentProcessedAsync/HandlePaymentFailedAsync — previously nothing in
    /// SaleOrderService consumed this topic at all.
    /// </summary>
    public class PaymentKafkaConsumer : IHostedService
    {
        private const string PaymentTopic = "payment-topic";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentKafkaConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public PaymentKafkaConsumer(IServiceProvider serviceProvider, ILogger<PaymentKafkaConsumer> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "sales-order-service-payment-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer.Subscribe(PaymentTopic);
            _executingTask = Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null) return;

            _cancellationTokenSource?.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));

            _consumer.Close();
            _consumer.Dispose();
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromSeconds(5));
                    if (result?.Message?.Value == null)
                        continue;

                    await DispatchAsync(result.Message.Value, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming from payment-topic");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from payment-topic");
                }
            }
        }

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var raw = JObject.Parse(json);
            var typeName = raw["$type"]?.ToString() ?? string.Empty;

            using var scope = _serviceProvider.CreateScope();
            var saga = scope.ServiceProvider.GetRequiredService<OrderFulfillmentSaga>();

            if (typeName.Contains("PaymentProcessedEvent"))
            {
                var orderId = raw.Value<Guid?>("OrderId") ?? Guid.Empty;
                var paymentId = raw.Value<Guid?>("PaymentId") ?? Guid.Empty;
                if (orderId == Guid.Empty || paymentId == Guid.Empty)
                    return;

                await saga.HandlePaymentProcessedAsync(orderId, paymentId, cancellationToken);
                await TryCreateCommissionAsync(scope.ServiceProvider, orderId, cancellationToken);
            }
            else if (typeName.Contains("PaymentFailedEvent"))
            {
                var orderId = raw.Value<Guid?>("OrderId") ?? Guid.Empty;
                var reason = raw.Value<string?>("FailureReason") ?? "Unknown payment failure";
                if (orderId == Guid.Empty)
                    return;

                await saga.HandlePaymentFailedAsync(orderId, reason, cancellationToken);
            }
            // Other payment-topic events (PaymentInitiatedEvent, PaymentAuthorizedEvent,
            // PaymentRefundedEvent) are not yet consumed by SaleOrderService.
        }

        /// <summary>
        /// Commission only fires on an actual payment (matches the business rule "no
        /// commission on a merely-confirmed order") and only for orders with a SalesRepId —
        /// most orders won't have one yet since it's optional. Idempotent: skips if a
        /// CommissionRecord already exists for this order (Kafka consumers can redeliver).
        /// </summary>
        private static async Task TryCreateCommissionAsync(IServiceProvider services, Guid orderId, CancellationToken ct)
        {
            var orders = services.GetRequiredService<ISalesOrderRepository>();
            var plans = services.GetRequiredService<ICommissionPlanRepository>();
            var records = services.GetRequiredService<ICommissionRecordRepository>();

            var order = await orders.GetByIdAsync(orderId, ct);
            if (order?.SalesRepId is not Guid salesRepId)
                return;

            if (await records.ExistsForOrderAsync(orderId, ct))
                return;

            var plan = await plans.GetFirstActiveAsync(ct);
            if (plan == null)
                return;

            var record = CommissionRecord.Create(plan.Id, salesRepId, orderId, order.TotalAmount * plan.RateValue);
            await records.AddAsync(record, ct);
        }
    }
}
