using ERP.WH.Application.Interfaces;
using ERP.WH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.WH.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background service that polls WarehouseDbContext.OutboxMessages and publishes pending
    /// Settings-changed events via ISettingsEventPublisher (Kafka). Runs every 5 seconds.
    /// Entirely separate from the RabbitMQ-based IEventPublisher/EventPublisher path used by
    /// inventory command handlers — this outbox/processor exists solely for the new
    /// Settings-changed publish flow and does not touch inventory event publishing.
    /// </summary>
    public class SettingsOutboxProcessor : BackgroundService
    {
        // Distinguishes this processor's rows from the Inventory/Warehouse domain-event
        // rows now also written to the same OutboxMessages table (see
        // WarehouseEventOutboxProcessor/WarehouseEventOutboxWriter) — without this filter,
        // this processor would also pick up and wrongly publish those rows to Kafka.
        private const string SettingsEventTypeName = "SettingsChangedIntegrationEvent";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SettingsOutboxProcessor> _logger;
        private readonly int _pollingIntervalMs = 5000; // 5 seconds

        public SettingsOutboxProcessor(IServiceProvider serviceProvider, ILogger<SettingsOutboxProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SettingsOutboxProcessor started. Polling interval: {PollingIntervalMs}ms", _pollingIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessages(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing settings outbox messages");
                }

                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }

            _logger.LogInformation("SettingsOutboxProcessor stopped");
        }

        private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<ISettingsEventPublisher>();

            var unprocessedMessages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null && m.Type == SettingsEventTypeName)
                .OrderBy(m => m.OccurredOnUtc)
                .ToListAsync(cancellationToken);

            if (!unprocessedMessages.Any())
            {
                return;
            }

            _logger.LogInformation("Found {MessageCount} unpublished settings outbox messages", unprocessedMessages.Count);

            foreach (var message in unprocessedMessages)
            {
                try
                {
                    var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                    var @event = JsonConvert.DeserializeObject(message.Content, settings);

                    if (@event == null)
                    {
                        _logger.LogWarning("Failed to deserialize settings event {EventId}: {Type}", message.Id, message.Type);
                        message.ProcessedOnUtc = DateTime.UtcNow;
                        message.Error = "Deserialization failed";
                        continue;
                    }

                    await publisher.PublishAsync(@event, cancellationToken);

                    message.ProcessedOnUtc = DateTime.UtcNow;
                    message.Error = null;

                    _logger.LogInformation("Published settings event {EventId} to Kafka: {Type}", message.Id, message.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error publishing settings event {EventId}: {Type}", message.Id, message.Type);
                    message.Error = ex.Message;
                    // Do not mark as processed - will retry on next poll
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
