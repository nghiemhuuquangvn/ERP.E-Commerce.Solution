using ERP.POS.Infrastructure.Persistence;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.POS.Infrastructure.BackgroundJobs;

/// <summary>
/// Drains POSDbContext.OutboxMessages onto Kafka ("pos-topic"). Previously absent
/// entirely — POSDbContext.SaveChangesAsync correctly wrote TransactionCreatedEvent/
/// CashShiftClosedEvent rows into the outbox, but with no processor to relay them,
/// every domain event this service ever raised sat in the DB forever. This is the
/// root cause behind "real-time inventory deduction" never firing: WarehouseService
/// can't consume an event that never reaches Kafka in the first place.
/// </summary>
public class OutboxProcessor : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = ExecuteAsync(_cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null) return;

        _cancellationTokenSource?.Cancel();
        await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<POSDbContext>();
                var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

                var unprocessed = await dbContext.OutboxMessages
                    .Where(m => m.ProcessedOnUtc == null
                        && (m.Error == null || !m.Error.StartsWith(OutboxEventDeserializer.DeadLetterPrefix)))
                    .OrderBy(m => m.OccurredOnUtc)
                    .Take(OutboxEventDeserializer.DefaultBatchSize)
                    .ToListAsync(cancellationToken);

                foreach (var message in unprocessed)
                {
                    try
                    {
                        // Never mark an undeserializable message as processed — it was not
                        // published. Dead-letter it so it stays visible for replay instead.
                        if (!OutboxEventDeserializer.TryDeserialize(message, out var @event, out var error))
                        {
                            _logger.LogError("Outbox message {MessageId} dead-lettered: {Error}", message.Id, error);
                            OutboxEventDeserializer.MarkDeadLettered(message, error!);
                            continue;
                        }

                        await messageBus.PublishAsync(@event!, cancellationToken);
                        message.ProcessedOnUtc = DateTime.UtcNow;
                        message.Error = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error publishing outbox message {MessageId}", message.Id);
                        message.Error = ex.Message;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in POS outbox processor");
            }

            await Task.Delay(5000, cancellationToken);
        }
    }
}
