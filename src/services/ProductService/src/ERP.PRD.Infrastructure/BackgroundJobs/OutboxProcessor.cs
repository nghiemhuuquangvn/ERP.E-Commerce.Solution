using ERP.PRD.Infrastructure.Persistence;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.PRD.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background service that polls the Outbox table and publishes pending events to Kafka.
    /// Runs every 5 seconds by default.
    /// Implements the Outbox Pattern for reliable event publishing.
    /// </summary>
    public class OutboxProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessor> _logger;
        private readonly int _pollingIntervalMs = 5000; // 5 seconds

        public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxProcessor started. Polling interval: {PollingIntervalMs}ms", _pollingIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessages(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox messages");
                }

                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }

            _logger.LogInformation("OutboxProcessor stopped");
        }

        private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            // Get all unpublished messages (ProcessedOnUtc IS NULL)
            var unprocessedMessages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null)
                .OrderBy(m => m.OccurredOnUtc)
                .ToListAsync(cancellationToken);

            if (!unprocessedMessages.Any())
            {
                return;
            }

            _logger.LogInformation("Found {MessageCount} unpublished outbox messages", unprocessedMessages.Count);

            foreach (var message in unprocessedMessages)
            {
                try
                {
                    // Deserialize event from JSON
                    var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                    var @event = JsonConvert.DeserializeObject(message.Content, settings);

                    if (@event == null)
                    {
                        _logger.LogWarning("Failed to deserialize event {EventId}: {Type}", message.Id, message.Type);
                        message.ProcessedOnUtc = DateTime.UtcNow;
                        message.Error = "Deserialization failed";
                        continue;
                    }

                    // Publish to Kafka
                    await messageBus.PublishAsync(@event, cancellationToken);

                    // Mark as processed
                    message.ProcessedOnUtc = DateTime.UtcNow;
                    message.Error = null;

                    _logger.LogInformation("Published event {EventId} to Kafka: {Type}", message.Id, message.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error publishing event {EventId}: {Type}", message.Id, message.Type);
                    message.Error = ex.Message;
                    // Do not mark as processed - will retry on next poll
                }
            }

            // Save all changes
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
