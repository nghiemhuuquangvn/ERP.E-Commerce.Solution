using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Infrastructure.Persistence;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.WH.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Polls the same OutboxMessages table as SettingsOutboxProcessor but publishes
    /// Inventory/Warehouse domain events to RabbitMQ's "warehouse_exchange" via
    /// IEventPublisher instead of Kafka. Rows are staged transactionally by
    /// WarehouseEventOutboxWriter inside command handlers — replaces the previous
    /// pattern of handlers calling IEventPublisher.PublishAsync directly right after
    /// SaveChanges (not atomic with the DB write). Distinguishes its rows from Settings
    /// rows purely by Type name (see SettingsOutboxProcessor's matching filter) — no
    /// schema change needed since the two event families never share a type name.
    /// </summary>
    public class WarehouseEventOutboxProcessor : BackgroundService
    {
        private const string SettingsEventTypeName = "SettingsChangedIntegrationEvent";
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WarehouseEventOutboxProcessor> _logger;
        private readonly int _pollingIntervalMs = 5000;

        public WarehouseEventOutboxProcessor(IServiceProvider serviceProvider, ILogger<WarehouseEventOutboxProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WarehouseEventOutboxProcessor started. Polling interval: {PollingIntervalMs}ms", _pollingIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessages(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing warehouse event outbox messages");
                }

                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }

            _logger.LogInformation("WarehouseEventOutboxProcessor stopped");
        }

        private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var unprocessedMessages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null && m.Type != SettingsEventTypeName
                    && (m.Error == null || !m.Error.StartsWith(OutboxEventDeserializer.DeadLetterPrefix)))
                .OrderBy(m => m.OccurredOnUtc)
                .Take(OutboxEventDeserializer.DefaultBatchSize)
                .ToListAsync(cancellationToken);

            if (!unprocessedMessages.Any())
                return;

            _logger.LogInformation("Found {MessageCount} unpublished warehouse event outbox messages", unprocessedMessages.Count);

            foreach (var message in unprocessedMessages)
            {
                try
                {
                    // Never mark an undeserializable message as processed — it was not
                    // published. Dead-letter it so it stays visible for replay instead.
                    if (!OutboxEventDeserializer.TryDeserialize(message, out var @event, out var error))
                    {
                        _logger.LogError("Dead-lettered warehouse event {EventId}: {Error}", message.Id, error);
                        OutboxEventDeserializer.MarkDeadLettered(message, error!);
                        continue;
                    }

                    await eventPublisher.PublishAsync(@event!, cancellationToken);

                    message.ProcessedOnUtc = DateTime.UtcNow;
                    message.Error = null;

                    _logger.LogInformation("Published warehouse event {EventId} to RabbitMQ: {Type}", message.Id, message.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error publishing warehouse event {EventId}: {Type}", message.Id, message.Type);
                    message.Error = ex.Message;
                    // Do not mark as processed - will retry on next poll
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
