using ERP.SO.Application.Interfaces;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background service that polls <see cref="SalesOrderDbContext.OutboxMessages"/> and
    /// publishes pending Settings-changed events via <see cref="ISettingsEventPublisher"/>
    /// (Kafka "sale-order-settings-topic"). Named distinctly from ProductService's
    /// OutboxProcessor since there is no pre-existing outbox processor in this service to
    /// collide with — this is a brand-new, isolated publish path that never touches
    /// RabbitMqMessageBus/IMessageBus or the "sales_order_events" exchange used by the
    /// order-fulfillment saga. Runs every 5 seconds by default.
    /// </summary>
    public class SettingsOutboxProcessor : BackgroundService
    {
        // Distinguishes this processor's rows from the SalesOrder/SalesQuotation domain-event
        // rows now also written to the same OutboxMessages table (see
        // SalesOrderEventOutboxProcessor/DomainEventOutboxWriter) — without this filter,
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
            var dbContext = scope.ServiceProvider.GetRequiredService<SalesOrderDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<ISettingsEventPublisher>();

            // Get all unpublished messages (ProcessedOnUtc IS NULL)
            var unprocessedMessages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null && m.Type == SettingsEventTypeName
                    && (m.Error == null || !m.Error.StartsWith(OutboxEventDeserializer.DeadLetterPrefix)))
                .OrderBy(m => m.OccurredOnUtc)
                .Take(OutboxEventDeserializer.DefaultBatchSize)
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
                    // Never mark an undeserializable message as processed — it was not
                    // published. Dead-letter it so it stays visible for replay instead.
                    if (!OutboxEventDeserializer.TryDeserialize(message, out var @event, out var error))
                    {
                        _logger.LogError("Dead-lettered settings event {EventId}: {Error}", message.Id, error);
                        OutboxEventDeserializer.MarkDeadLettered(message, error!);
                        continue;
                    }

                    await publisher.PublishAsync(@event!, cancellationToken);

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
