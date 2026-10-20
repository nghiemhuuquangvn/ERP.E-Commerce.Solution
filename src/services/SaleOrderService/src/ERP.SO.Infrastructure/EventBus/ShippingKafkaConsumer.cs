using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
using Confluent.Kafka;
using ERP.SO.Application.Sagas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.SO.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes ShippingService's "shipping-topic" (Outbox pattern, Newtonsoft
    /// TypeNameHandling.All — dispatch by "$type" substring, mirrors
    /// PaymentKafkaConsumer/ProductPriceChangedConsumer). Feeds OrderFulfillmentSaga's
    /// HandleShipmentCreatedAsync — previously nothing in SaleOrderService consumed
    /// this topic at all.
    /// </summary>
    public class ShippingKafkaConsumer : IHostedService
    {
        private const string ShippingTopic = "shipping-topic";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ShippingKafkaConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public ShippingKafkaConsumer(IServiceProvider serviceProvider, ILogger<ShippingKafkaConsumer> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "sales-order-service-shipping-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer.Subscribe(ShippingTopic);
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
                    _logger.LogError(ex, "Error consuming from shipping-topic");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from shipping-topic");
                }
            }
        }

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var raw = JObject.Parse(json);
            var typeName = raw["$type"]?.ToString() ?? string.Empty;
            if (!typeName.Contains("ShipmentCreatedEvent"))
                return; // Other shipping-topic events are not yet consumed by SaleOrderService.

            var orderId = raw.Value<Guid?>("OrderId") ?? Guid.Empty;
            var shipmentId = raw.Value<Guid?>("ShipmentId") ?? Guid.Empty;
            if (orderId == Guid.Empty || shipmentId == Guid.Empty)
                return;

            using var scope = _serviceProvider.CreateScope();
            var saga = scope.ServiceProvider.GetRequiredService<OrderFulfillmentSaga>();
            if (await OrderTenant.ResolveAsync(scope.ServiceProvider.GetRequiredService<SalesOrderDbContext>(), orderId, json, _logger, cancellationToken) is not { } tenantId)
                return;
            using var tenantScope = TenantScope.Begin(tenantId);
            await saga.HandleShipmentCreatedAsync(orderId, shipmentId, cancellationToken);
        }
    }
}
