using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
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
        private const string WarehouseTopic = "warehouse-topic";

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
            _consumer.Subscribe(new[] { PurchasingTopic, PaymentTopic, WarehouseTopic });
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

        /// <summary>PaymentService serialises its PaymentMethod enum as a number (CreditCard 0 ... Cash 4); accept the name too.</summary>
        public static int ParsePaymentMethod(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return 0;
            if (token.Type == JTokenType.Integer)
                return token.Value<int>();
            return token.ToString().ToLowerInvariant() switch
            {
                "creditcard" => 0,
                "debitcard" => 1,
                "banktransfer" => 2,
                "ewallet" => 3,
                "cash" => 4,
                "cryptopayment" or "crypto" => 5,
                _ => 0
            };
        }

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

            // Ledger entries belong to the tenant of the source document; an event without
            // one is dropped (fail closed), never posted to some other tenant's books.
            if (!EventTenant.TryRead(json, out var tenantId))
            {
                _logger.LogWarning("{EventType} without TenantId ignored", typeName);
                return;
            }
            using var tenantScope = TenantScope.Begin(tenantId);

            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            if (typeName.Contains("PurchaseOrderApprovedDomainEvent"))
            {
                // Final approval (manager, or finance above the threshold) carries the order
                // total; the bill is booked then. PurchaseOrderCreated has no lines yet.
                var raw = JObject.Parse(json);
                var evt = new PurchaseOrderApprovedIntegrationEvent
                {
                    POId = raw.GuidValue("PurchaseOrderId") ?? Guid.Empty,
                    PONumber = raw.Value<string?>("PONumber") ?? string.Empty,
                    SupplierId = raw.GuidValue("SupplierId") ?? Guid.Empty,
                    TotalAmount = raw.Value<decimal?>("TotalAmount") ?? 0m
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
                    PaymentId = raw.GuidValue("PaymentId") ?? Guid.Empty,
                    OrderId = raw.GuidValue("OrderId") ?? Guid.Empty,
                    Amount = raw.Value<decimal?>("Amount") ?? 0m,
                    Method = ParsePaymentMethod(raw["Method"]),
                    ProcessedAt = raw["ProcessedAt"]?.Type == JTokenType.Null ? null : raw.Value<DateTime?>("ProcessedAt")
                };

                if (evt.PaymentId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("StockMoved") || typeName.Contains("InventoryMoved") || typeName.Contains("WarehouseMovement"))
            {
                var raw = JObject.Parse(json);
                var evt = new WarehouseMovementIntegrationEvent
                {
                    MovementId = raw.GuidValue("MovementId") ?? raw.GuidValue("Id") ?? Guid.Empty,
                    MovementType = raw.Value<string?>("MovementType") ?? "Unknown",
                    ItemId = raw.GuidValue("ItemId") ?? raw.GuidValue("ProductId") ?? Guid.Empty,
                    Quantity = raw.Value<int?>("Quantity") ?? 0,
                    UnitCost = raw.Value<decimal?>("UnitCost") ?? 0m,
                    TotalValue = raw.Value<decimal?>("TotalValue") ?? raw.Value<decimal?>("TotalAmount") ?? 0m
                };

                if (evt.MovementId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            // Other topic events are not yet consumed by Accounting.
        }
    }
}
