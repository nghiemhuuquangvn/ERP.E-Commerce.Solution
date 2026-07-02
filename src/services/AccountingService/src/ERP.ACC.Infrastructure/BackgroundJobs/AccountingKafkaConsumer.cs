using Confluent.Kafka;
using ERP.ACC.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.ACC.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Consumes PurchaseOrderService's "purchasing-topic" and PaymentService's
    /// "payment-topic" — both publish via the Outbox pattern with Newtonsoft
    /// TypeNameHandling.All, so the "$type" discriminator holds the publisher's fully
    /// qualified CLR type name. Dispatch is by substring match against that, mirroring
    /// HRMService's MasterDataEventConsumer. Accounting's existing event handlers are
    /// real MediatR INotificationHandler implementations, so dispatch is via
    /// IMediator.Publish.
    /// </summary>
    public class AccountingKafkaConsumer : IHostedService
    {
        private const string PurchasingTopic = "purchasing-topic";
        private const string PaymentTopic = "payment-topic";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AccountingKafkaConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public AccountingKafkaConsumer(
            IServiceProvider serviceProvider,
            ILogger<AccountingKafkaConsumer> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "accounting-service-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer.Subscribe(new[] { PurchasingTopic, PaymentTopic });
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
                    _logger.LogError(ex, "Error consuming from purchasing-topic/payment-topic");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from purchasing-topic/payment-topic");
                }
            }
        }

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            if (typeName.Contains("PurchaseOrderCreatedDomainEvent"))
            {
                // PurchaseOrderCreatedDomainEvent carries (PurchaseOrderId, PONumber,
                // SupplierId) — no TotalAmount, so the created Bill's Amount defaults to
                // 0 rather than guessing; matches this codebase's established
                // safe-default-for-unavailable-field pattern (e.g. CRM's identity-topic
                // consumer defaulting Contact fields it can't resolve).
                var raw = JObject.Parse(json);
                var evt = new PurchaseOrderCreatedIntegrationEvent
                {
                    POId = raw.Value<Guid?>("PurchaseOrderId") ?? Guid.Empty,
                    SupplierId = raw.Value<Guid?>("SupplierId") ?? Guid.Empty,
                    TotalAmount = 0
                };

                if (evt.POId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("PaymentProcessedEvent"))
            {
                // PaymentService's PaymentProcessedEvent(PaymentId, OrderId, Amount, ...)
                // maps 1:1 onto the local PaymentProcessedIntegrationEvent.
                var raw = JObject.Parse(json);
                var evt = new PaymentProcessedIntegrationEvent
                {
                    PaymentId = raw.Value<Guid?>("PaymentId") ?? Guid.Empty,
                    OrderId = raw.Value<Guid?>("OrderId") ?? Guid.Empty,
                    Amount = raw.Value<decimal?>("Amount") ?? 0m
                };

                if (evt.PaymentId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            // Other purchasing-topic/payment-topic events are not yet consumed by
            // Accounting — no local handler exists for them today.
        }
    }
}
