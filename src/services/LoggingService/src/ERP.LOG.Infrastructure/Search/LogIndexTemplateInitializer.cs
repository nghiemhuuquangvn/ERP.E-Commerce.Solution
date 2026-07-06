using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Elastic.Clients.Elasticsearch;

namespace ERP.LOG.Infrastructure.Search;

/// <summary>
/// Ensures an Elasticsearch index template for "service-logs-*" exists on startup, so every
/// monthly rolling index (service-logs-2026.07, service-logs-2026.08, ...) is created with a
/// consistent mapping without manual per-month setup.
/// </summary>
public class LogIndexTemplateInitializer : IHostedService
{
    private const string TemplateName = "service-logs-template";

    private readonly ElasticsearchClient _client;
    private readonly string _indexPrefix;
    private readonly ILogger<LogIndexTemplateInitializer> _logger;

    public LogIndexTemplateInitializer(
        ElasticsearchClient client,
        IConfiguration configuration,
        ILogger<LogIndexTemplateInitializer> logger)
    {
        _client = client;
        _indexPrefix = configuration.GetValue<string>("Elasticsearch:IndexPrefix") ?? "service-logs";
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.Indices.PutIndexTemplateAsync(TemplateName, r => r
                .IndexPatterns($"{_indexPrefix}-*")
                .Template(t => t
                    .Mappings(m => m
                        .Properties<object>(p => p
                            .Keyword("serviceName")
                            .Keyword("environment")
                            .Keyword("level")
                            .Keyword("kind")
                            .Text("message", tx => tx.Fields(f => f.Keyword("keyword")))
                            .Keyword("exceptionType")
                            .Text("stackTrace")
                            .Keyword("correlationId")
                            .Keyword("source")
                            .Date("occurredAt")
                            .Date("indexedAt")))),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Failed to ensure Elasticsearch index template {Template}: {Reason}",
                    TemplateName, response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error ensuring Elasticsearch index template {Template} — service will still start", TemplateName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
