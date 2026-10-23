using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Confluent.Kafka;
using ERP.PRM.Application.EventHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.PRM.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Consumes ProductService's "product-topic" (Outbox pattern, Newtonsoft
    /// TypeNameHandling.All, "$type" discriminator) — mirrors HRMService's
    /// MasterDataEventConsumer pattern. Dispatches to the existing (previously dead)
    /// ProductCreatedForPromotionHandler, resolved from a DI scope and invoked directly
    /// since it's a plain class (HandleAsync), not a MediatR INotificationHandler.
    /// </summary>
    public class PromotionKafkaConsumer : IHostedService
    {
        private const string ProductTopic = "product-topic";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PromotionKafkaConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public PromotionKafkaConsumer(
            IServiceProvider serviceProvider,
            ILogger<PromotionKafkaConsumer> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "promotion-service-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer.Subscribe(ProductTopic);
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
                    _logger.LogError(ex, "Error consuming from product-topic");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from product-topic");
                }
            }
        }

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

            // ProductCreated carries the product's tenant (stamped by ProductService); without one nothing is
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
                // Category/Price, so the cache row is seeded with safe placeholders
                // (PromotionProductCache.Category is NOT NULL) rather than guessed
                // data; ProductPriceChangedEvent consumption (a further gap) would be
                // the natural place to backfill Price once wired.
                var raw = JObject.Parse(json);
                var productId = raw.GuidValue("ProductId") ?? Guid.Empty;
                var name = raw.Value<string>("Name") ?? string.Empty;

                if (productId == Guid.Empty)
                    return;

                var evt = new ProductCreatedIntegrationEvent(productId, name, "Uncategorized", 0m);
                var handler = scope.ServiceProvider.GetRequiredService<ProductCreatedForPromotionHandler>();
                await handler.HandleAsync(evt, cancellationToken);
            }
            // Other product-topic events are not yet consumed by Promotion — no local
            // handler exists for them today.
        }
    }
}
