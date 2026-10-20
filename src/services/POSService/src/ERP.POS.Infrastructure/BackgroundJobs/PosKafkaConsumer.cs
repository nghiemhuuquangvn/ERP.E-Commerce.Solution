using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Confluent.Kafka;
using ERP.POS.Application.EventHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.POS.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Consumes ProductService's "product-topic", PromotionService's
    /// "promotion-topic", and PaymentService's "payment-topic" (all Outbox pattern,
    /// Newtonsoft TypeNameHandling.All, "$type" discriminator) — mirrors HRMService's
    /// MasterDataEventConsumer. Dispatches to the existing (previously dead) POS
    /// integration event handlers, resolved from a DI scope and invoked directly
    /// since they are plain classes (HandleAsync), not MediatR notification handlers.
    /// </summary>
    public class PosKafkaConsumer : IHostedService
    {
        private const string ProductTopic = "product-topic";
        private const string PromotionTopic = "promotion-topic";
        private const string PaymentTopic = "payment-topic";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PosKafkaConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public PosKafkaConsumer(
            IServiceProvider serviceProvider,
            ILogger<PosKafkaConsumer> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "pos-service-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer.Subscribe(new[] { ProductTopic, PromotionTopic, PaymentTopic });
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
                    _logger.LogError(ex, "Error consuming from product-topic/promotion-topic");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from product-topic/promotion-topic");
                }
            }
        }

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

            // Product/Promotion/Payment events carry their tenant (stamped by the publishing service); without one nothing is
            // changed (fail closed).
            if (!EventTenant.TryRead(json, out var tenantId))
            {
                _logger.LogWarning("{EventType} without TenantId ignored", typeName);
                return;
            }
            using var tenantScope = TenantScope.Begin(tenantId);

            using var scope = _serviceProvider.CreateScope();

            if (typeName.Contains("ProductCreatedDomainEvent"))
            {
                // ProductCreatedDomainEvent only carries (ProductId, Name, Sku) — no
                // Price/IsActive, so those default (Price=0, IsActive=true) rather
                // than being guessed; the real price arrives via
                // ProductPriceChangedDomainEvent, handled below.
                var raw = JObject.Parse(json);
                var productId = raw.Value<Guid?>("ProductId") ?? Guid.Empty;
                var name = raw.Value<string>("Name") ?? string.Empty;
                var sku = raw.Value<string>("Sku") ?? string.Empty;

                if (productId == Guid.Empty)
                    return;

                var evt = new ProductCreatedIntegrationEvent(productId, name, sku, 0m, true);
                var handler = scope.ServiceProvider.GetRequiredService<ProductCreatedForPosHandler>();
                await handler.HandleAsync(evt, cancellationToken);
            }
            else if (typeName.Contains("ProductPriceChangedDomainEvent"))
            {
                // ProductPriceChangedForPosHandler already existed and correctly
                // updated the cache, but was never registered in DI nor dispatched
                // here — dead code because ProductService had no such event to
                // publish at the time. ProductService added it in section 3.2
                // (ProductVariant.RecordPriceChange), so this is now reachable.
                var raw = JObject.Parse(json);
                var productId = raw.Value<Guid?>("ProductId") ?? Guid.Empty;
                var newPrice = raw.Value<decimal?>("NewPrice") ?? 0m;
                if (productId == Guid.Empty)
                    return;

                var evt = new ProductPriceChangedIntegrationEvent(productId, raw.Value<decimal?>("OldPrice") ?? 0m, newPrice);
                var handler = scope.ServiceProvider.GetRequiredService<ProductPriceChangedForPosHandler>();
                await handler.HandleAsync(evt, cancellationToken);
            }
            else if (typeName.Contains("PromotionCreatedEvent"))
            {
                // PromotionCreatedEvent(PromotionId, Name, DiscountType (enum),
                // DiscountValue, StartDate, EndDate) — DiscountType is serialized as
                // its underlying int by Newtonsoft, matching the local contract's
                // int field directly.
                var raw = JObject.Parse(json);
                var evt = new PromotionCreatedIntegrationEvent(
                    raw.Value<Guid?>("PromotionId") ?? Guid.Empty,
                    raw.Value<string>("Name") ?? string.Empty,
                    raw.Value<int?>("DiscountType") ?? 0,
                    raw.Value<decimal?>("DiscountValue") ?? 0m,
                    raw.Value<DateTime?>("StartDate") ?? DateTime.UtcNow,
                    raw.Value<DateTime?>("EndDate") ?? DateTime.UtcNow);

                if (evt.PromotionId != Guid.Empty)
                {
                    var handler = scope.ServiceProvider.GetRequiredService<PromotionCreatedForPosHandler>();
                    await handler.HandleAsync(evt, cancellationToken);
                }
            }
            else if (typeName.Contains("PaymentProcessedEvent"))
            {
                // PaymentProcessedEvent(PaymentId, OrderId, Amount, Currency, Method,
                // ProcessedAt) — Method is PaymentService's own enum, serialized as
                // its underlying int; POS's local contract just carries it as a
                // string for display/logging, so ToString() the raw int is enough.
                var raw = JObject.Parse(json);
                var paymentId = raw.Value<Guid?>("PaymentId") ?? Guid.Empty;
                var orderId = raw.Value<Guid?>("OrderId") ?? Guid.Empty;
                if (paymentId == Guid.Empty || orderId == Guid.Empty)
                    return;

                var evt = new PaymentProcessedIntegrationEvent(
                    paymentId, orderId,
                    raw.Value<decimal?>("Amount") ?? 0m,
                    raw.Value<string>("Method") ?? string.Empty);

                var handler = scope.ServiceProvider.GetRequiredService<PaymentProcessedForPosHandler>();
                await handler.HandleAsync(evt, cancellationToken);
            }
            // Other product-topic/promotion-topic/payment-topic events are not yet
            // consumed by POS — no local handler exists for them today.
        }
    }
}
