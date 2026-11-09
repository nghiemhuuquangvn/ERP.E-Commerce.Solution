using ERP.MFG.Infrastructure.Persistence;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.MFG.Infrastructure.BackgroundJobs;

/// <summary>
/// Publishes the outbox rows to Kafka (shared Outbox rules): oldest first in batches, a message that cannot be restored is dead-lettered and stays visible,
/// a publish failure keeps the error and is retried on the next round, <c>ProcessedOnUtc</c> is only set for a message that was published.
/// </summary>
public sealed class OutboxProcessor(IServiceProvider services, ILogger<OutboxProcessor> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in outbox processor");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MfgDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && (m.Error == null || !m.Error.StartsWith(OutboxEventDeserializer.DeadLetterPrefix)))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(OutboxEventDeserializer.DefaultBatchSize)
            .ToListAsync(ct);

        var published = 0;
        foreach (var message in pending)
        {
            try
            {
                if (!OutboxEventDeserializer.TryDeserialize(message, out var domainEvent, out var error))
                {
                    logger.LogError("Outbox message {MessageId} dead-lettered: {Error}", message.Id, error);
                    OutboxEventDeserializer.MarkDeadLettered(message, error!);
                    continue;
                }

                await bus.PublishAsync(domainEvent!, ct);
                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error publishing outbox message {MessageId}", message.Id);
                message.Error = ex.Message;
            }
        }

        await db.SaveChangesAsync(ct);
        return published;
    }
}
