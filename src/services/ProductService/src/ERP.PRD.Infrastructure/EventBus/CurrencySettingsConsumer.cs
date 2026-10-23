using ERP.Shared.Messaging;
using Confluent.Kafka;
using ERP.Shared.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.PRD.Infrastructure.EventBus
{
    /// <summary>
    /// Consumes SharedMasterDataService's "mst-topic" (Outbox pattern, Newtonsoft
    /// TypeNameHandling.All — dispatch by "$type" substring, mirrors
    /// SaleOrderService.ProductPriceChangedConsumer / HRMService.MasterDataEventConsumer).
    /// Only reacts to SettingsChangedIntegrationEvent messages where
    /// ServiceName == "SharedMasterDataService" and Category == "Currency"; mst-topic
    /// also carries other MST events (e.g. DepartmentCreated) which are ignored here.
    ///
    /// This consumer is forward-compatible plumbing: ProductService does not yet
    /// fetch/cache Currency Settings from SharedMasterDataService anywhere, so this
    /// consumer currently has no visible effect until that integration exists. It
    /// exists so the cache-invalidation wiring is already correct and ready for when
    /// ProductService starts caching Currency Settings under the
    /// "currency-settings:system" / "currency-settings:tenant:{tenantId}" keys.
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
                GroupId = "product-service-consumer",
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
                return; // Other mst-topic events (e.g. DepartmentCreated) are not handled here.

            var serviceName = raw.Value<string?>("ServiceName");
            var category = raw.Value<string?>("Category");
            if (serviceName != "SharedMasterDataService" || category != "Currency")
                return;

            var scope = raw.Value<string?>("Scope");
            var tenantId = raw.GuidValue("TenantId");

            using var scopeProvider = _serviceProvider.CreateScope();
            var cacheService = scopeProvider.ServiceProvider.GetRequiredService<ICacheService>();

            if (scope == "System")
            {
                await cacheService.RemoveAsync("currency-settings:system", cancellationToken);
            }
            else if (scope == "Tenant" && tenantId.HasValue)
            {
                await cacheService.RemoveAsync($"currency-settings:tenant:{tenantId}", cancellationToken);
            }
        }
    }
}
