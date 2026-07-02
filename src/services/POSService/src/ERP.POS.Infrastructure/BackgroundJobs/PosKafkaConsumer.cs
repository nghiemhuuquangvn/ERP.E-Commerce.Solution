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
    /// Consumes ProductService's "product-topic" and PromotionService's
    /// "promotion-topic" (both Outbox pattern, Newtonsoft TypeNameHandling.All,
    /// "$type" discriminator) — mirrors HRMService's MasterDataEventConsumer.
    /// Dispatches to the existing (previously dead) POS integration event handlers,
    /// resolved from a DI scope and invoked directly since they are plain classes
    /// (HandleAsync), not MediatR notification handlers.
    /// </summary>
    public class PosKafkaConsumer : IHostedService
    {
        private const string ProductTopic = "product-topic";
        private const string PromotionTopic = "promotion-topic";

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
            _consumer.Subscribe(new[] { ProductTopic, PromotionTopic });
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

            using var scope = _serviceProvider.CreateScope();

            if (typeName.Contains("ProductCreatedDomainEvent"))
            {
                // ProductCreatedDomainEvent only carries (ProductId, Name, Sku) — no
                // Price/IsActive, so those default (Price=0, IsActive=true) rather
                // than being guessed; ProductPriceChangedIntegrationEvent is NOT
                // wired here because no corresponding domain event exists in
                // ProductService yet (confirmed — a real, separate gap, not
                // something this consumer can source data for).
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
            // Other product-topic/promotion-topic events are not yet consumed by
            // POS — no local handler exists for them today.
        }
    }
}
