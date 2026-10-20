using ERP.Shared.Infrastructure.Data;
using Elastic.Clients.Elasticsearch;
using ERP.PRD.Domain.Search;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.PRD.Infrastructure.Search
{
    /// <summary>
    /// Background service that polls the SearchIndexQueue table and rebuilds the derived
    /// Elasticsearch "products" document for each pending ProductId. Runs every 5 seconds by
    /// default. Mirrors OutboxProcessor's exact polling/try-catch-per-item shape: SQL is the
    /// system-of-record, this just keeps the read-optimized ES index eventually consistent
    /// with it.
    /// </summary>
    public class SearchIndexProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SearchIndexProcessor> _logger;
        private readonly IConfiguration _configuration;
        private readonly int _pollingIntervalMs = 5000; // 5 seconds

        public SearchIndexProcessor(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<SearchIndexProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SearchIndexProcessor started. Polling interval: {PollingIntervalMs}ms", _pollingIntervalMs);

            await EnsureIndexExistsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSearchIndexQueue(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing search index queue");
                }

                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }

            _logger.LogInformation("SearchIndexProcessor stopped");
        }

        /// <summary>
        /// Creates the "products" ES index on first startup if it doesn't already exist.
        /// Relies on Elasticsearch's default dynamic mapping to infer a field type per
        /// attribute code from the natively-typed values ElasticsearchProductIndexer sends
        /// (numbers/booleans/strings) rather than a hand-rolled dynamic template — a
        /// pragmatic, sufficient approach per the design goal of "any attribute becomes
        /// filterable/aggregatable", not a fully generic mapping framework.
        /// </summary>
        private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
                var indexName = _configuration.GetValue<string>("Elasticsearch:IndexName") ?? "products";

                var existsResponse = await client.Indices.ExistsAsync(indexName, cancellationToken);
                if (!existsResponse.Exists)
                {
                    var createResponse = await client.Indices.CreateAsync(indexName, cancellationToken);
                    if (createResponse.IsValidResponse)
                        _logger.LogInformation("Created Elasticsearch index {IndexName}", indexName);
                    else
                        _logger.LogWarning("Failed to create Elasticsearch index {IndexName}: {DebugInfo}", indexName, createResponse.DebugInformation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring Elasticsearch index exists");
            }
        }

        private async Task ProcessSearchIndexQueue(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            var indexer = scope.ServiceProvider.GetRequiredService<IProductSearchIndexer>();

            // Get all unindexed rows (IndexedAt IS NULL)
            var pendingEntries = await dbContext.SearchIndexQueue
                .Where(q => q.IndexedAt == null)
                .OrderBy(q => q.RequestedAt)
                .ToListAsync(cancellationToken);

            if (!pendingEntries.Any())
            {
                return;
            }

            _logger.LogInformation("Found {Count} pending search index entries", pendingEntries.Count);

            foreach (var entry in pendingEntries)
            {
                try
                {
                    // No user here: index each product inside its own tenant (looked up across
                    // tenants). Unknown product = deleted -> no scope, the indexer removes the doc.
                    var tenantId = await dbContext.Products
                        .IgnoreQueryFilters()
                        .Where(p => p.Id == entry.ProductId)
                        .Select(p => (Guid?)p.TenantId)
                        .FirstOrDefaultAsync(cancellationToken);

                    using (tenantId is { } t && t != Guid.Empty ? TenantScope.Begin(t) : null)
                        await indexer.IndexAsync(entry.ProductId, cancellationToken);

                    entry.IndexedAt = DateTime.UtcNow;
                    entry.Error = null;

                    _logger.LogInformation("Indexed product {ProductId} for search", entry.ProductId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error indexing product {ProductId} for search", entry.ProductId);
                    entry.Error = ex.Message;
                    // Do not mark as indexed - will retry on next poll
                }
            }

            // Save all changes
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
