using ERP.Shared.Messaging;
using ERP.SO.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.SO.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Polls the same <see cref="SalesOrderDbContext.OutboxMessages"/> table as
    /// <see cref="SettingsOutboxProcessor"/> but publishes SalesOrder/SalesQuotation domain
    /// events to RabbitMQ's "sales_order_events" exchange via <see cref="IMessageBus"/>
    /// instead of Kafka. Rows are staged transactionally by
    /// <see cref="DomainEventOutboxWriter"/> inside SalesOrderRepository/
    /// SalesQuotationRepository — this replaces the previous pattern of command handlers
    /// calling IMessageBus.PublishAsync directly right after SaveChanges (not atomic with
    /// the DB write). Distinguishes its rows from Settings rows purely by Type name (see
    /// SettingsOutboxProcessor's matching filter) — no schema change needed since the two
    /// event families never share a type name.
    /// </summary>
    public class SalesOrderEventOutboxProcessor : BackgroundService
    {
        private const string SettingsEventTypeName = "SettingsChangedIntegrationEvent";
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SalesOrderEventOutboxProcessor> _logger;
        private readonly int _pollingIntervalMs = 5000;

        public SalesOrderEventOutboxProcessor(IServiceProvider serviceProvider, ILogger<SalesOrderEventOutboxProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SalesOrderEventOutboxProcessor started. Polling interval: {PollingIntervalMs}ms", _pollingIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessages(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing sales order event outbox messages");
                }

                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }

            _logger.LogInformation("SalesOrderEventOutboxProcessor stopped");
        }

        private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SalesOrderDbContext>();
            var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            var unprocessedMessages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null && m.Type != SettingsEventTypeName)
                .OrderBy(m => m.OccurredOnUtc)
                .ToListAsync(cancellationToken);

            if (!unprocessedMessages.Any())
                return;

            _logger.LogInformation("Found {MessageCount} unpublished sales order event outbox messages", unprocessedMessages.Count);

            foreach (var message in unprocessedMessages)
            {
                try
                {
                    var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                    var @event = JsonConvert.DeserializeObject(message.Content, settings);

                    if (@event == null)
                    {
                        _logger.LogWarning("Failed to deserialize sales order event {EventId}: {Type}", message.Id, message.Type);
                        message.ProcessedOnUtc = DateTime.UtcNow;
                        message.Error = "Deserialization failed";
                        continue;
                    }

                    await messageBus.PublishAsync(@event, cancellationToken);

                    message.ProcessedOnUtc = DateTime.UtcNow;
                    message.Error = null;

                    _logger.LogInformation("Published sales order event {EventId} to RabbitMQ: {Type}", message.Id, message.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error publishing sales order event {EventId}: {Type}", message.Id, message.Type);
                    message.Error = ex.Message;
                    // Do not mark as processed - will retry on next poll
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
