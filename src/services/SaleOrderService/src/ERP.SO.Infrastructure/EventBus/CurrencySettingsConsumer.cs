using Confluent.Kafka;
using ERP.Shared.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.SO.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes SharedMasterDataService's existing outbox topic ("mst-topic"), filtering
    /// for SettingsChangedIntegrationEvent messages where ServiceName ==
    /// "SharedMasterDataService" and Category == "Currency", and invalidates the
    /// corresponding local cache entry ("currency-settings:system" or
    /// "currency-settings:tenant:{tenantId}") so a future read picks up fresh data.
    ///
    /// This is forward-compatible plumbing only: SaleOrderService does not yet fetch or
    /// cache Currency Settings from SharedMasterDataService anywhere, so this consumer has
    /// no visible effect until that integration exists. Structurally mirrors
    /// <see cref="ProductPriceChangedConsumer"/> (same IHostedService+IConsumer shape),
    /// but uses a distinct consumer group id ("sales-order-currency-settings-consumer")
    /// so it does not compete for partition offsets with the
    /// "sales-order-service-consumer" group used by the other consumers in this service.
    /// </summary>
    public class CurrencySettingsConsumer : IHostedService
    {
        private const string MstTopic = "mst-topic";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CurrencySettingsConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public CurrencySettingsConsumer(IServiceProvider serviceProvider, ILogger<CurrencySettingsConsumer> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "sales-order-currency-settings-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer.Subscribe(MstTopic);
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
                    _logger.LogError(ex, "Error consuming from mst-topic");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from mst-topic");
                }
            }
        }

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var raw = JObject.Parse(json);
            var typeName = raw["$type"]?.ToString() ?? string.Empty;
            if (!typeName.Contains("SettingsChangedIntegrationEvent"))
                return; // Other mst-topic events are not yet consumed by SaleOrderService.

            var serviceName = raw.Value<string>("ServiceName");
            var category = raw.Value<string>("Category");
            var settingsScope = raw.Value<string>("Scope");
            var tenantId = raw.Value<Guid?>("TenantId");

            if (serviceName != "SharedMasterDataService" || category != "Currency")
                return; // Only Currency Settings from SharedMasterDataService are relevant here.

            using var scope = _serviceProvider.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            if (settingsScope == "System")
            {
                await cache.RemoveAsync("currency-settings:system", cancellationToken);
            }
            else if (settingsScope == "Tenant" && tenantId.HasValue)
            {
                await cache.RemoveAsync($"currency-settings:tenant:{tenantId.Value}", cancellationToken);
            }
        }
    }
}
