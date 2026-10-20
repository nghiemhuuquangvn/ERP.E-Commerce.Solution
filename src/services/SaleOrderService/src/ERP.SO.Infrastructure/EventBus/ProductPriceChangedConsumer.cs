using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
using Confluent.Kafka;
using ERP.SO.Application.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.SO.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes ProductService's "product-topic" (Outbox pattern, Newtonsoft
    /// TypeNameHandling.All — dispatch by "$type" substring, mirrors
    /// HRMService.MasterDataEventConsumer/AccountingKafkaConsumer). Previously
    /// impossible to build at all: ProductPriceChangedDomainEvent didn't exist
    /// anywhere in ProductService until 2026-07-03 (see docs/business section 3.2).
    /// Flags any open (not yet shipped/delivered/cancelled) order still holding the
    /// affected product, rather than silently doing nothing — SaleOrderService does
    /// not retroactively change a price a customer already agreed to.
    /// </summary>
    public class ProductPriceChangedConsumer : IHostedService
    {
        private const string ProductTopic = "product-topic";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProductPriceChangedConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public ProductPriceChangedConsumer(IServiceProvider serviceProvider, ILogger<ProductPriceChangedConsumer> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "sales-order-service-consumer",
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
            if (!typeName.Contains("ProductPriceChangedDomainEvent"))
                return; // Other product-topic events are not yet consumed by SaleOrderService.

            var raw = JObject.Parse(json);
            var productId = raw.Value<Guid?>("ProductId") ?? Guid.Empty;
            var oldPrice = raw.Value<decimal?>("OldPrice") ?? 0m;
            var newPrice = raw.Value<decimal?>("NewPrice") ?? 0m;
            if (productId == Guid.Empty)
                return;
            // Product ids are only meaningful inside the product's tenant; without one we
            // must not touch any order (fail closed).
            if (!EventTenant.TryRead(json, out var tenantId))
            {
                _logger.LogWarning("ProductPriceChanged for {ProductId} has no TenantId — ignored", productId);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISalesOrderRepository>();
            using var tenantScope = TenantScope.Begin(tenantId);

            var openOrders = await repository.GetOpenOrdersContainingProductAsync(productId, cancellationToken);
            foreach (var order in openOrders)
            {
                order.FlagPriceChanged(productId, oldPrice, newPrice);
                await repository.UpdateAsync(order, cancellationToken);
            }
        }
    }
}
