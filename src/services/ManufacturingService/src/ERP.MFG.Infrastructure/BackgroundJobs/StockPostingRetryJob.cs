using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.MFG.Infrastructure.BackgroundJobs;

/// <summary>
/// Finishes the stock documents a request could not: every Pending document whose next attempt is due is sent to the Warehouse again (or reversed, in Rollback mode).
/// No user here: documents are found across tenants, then each one is handled inside its own tenant scope and DI scope. A document is <b>claimed</b> first - its next
/// attempt is pushed two minutes ahead and saved - so a second instance (or the request itself) that read the same row loses the save (row version) and leaves it alone.
/// All calls are idempotent, so even a lost claim cannot move stock twice. A transient failure never fails the document: it stays Pending and the attempts / error are the alarm.
/// </summary>
public sealed class StockPostingRetryJob(IServiceProvider services, ILogger<StockPostingRetryJob> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;
    private const string System = "system";

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
                logger.LogError(ex, "Error in the stock posting retry job");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>One round; returns how many documents reached Posted or Failed.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        List<(Guid TenantId, Guid DocumentId)> due;
        using (var scope = services.CreateScope())
            due = await scope.ServiceProvider.GetRequiredService<IStockDocumentRepository>().ListDueAsync(now, BatchSize, ct);

        var finished = 0;
        foreach (var (tenantId, documentId) in due.Where(d => d.TenantId != Guid.Empty))
        {
            using (TenantScope.Begin(tenantId))
            using (var scope = services.CreateScope())
            {
                try
                {
                    var documents = scope.ServiceProvider.GetRequiredService<IStockDocumentRepository>();
                    var document = await documents.GetAsync(documentId, ct);
                    if (document is null || document.Status != StockDocumentStatus.Pending || (document.NextAttemptAt is { } next && next > now)) continue;

                    document.Claim(now + StockRetryPolicy.Lease);
                    await scope.ServiceProvider.GetRequiredService<IMfgUnitOfWork>().SaveChangesAsync(ct);

                    var result = await scope.ServiceProvider.GetRequiredService<IStockPostingService>().PostAsync(document, System, ct);
                    if (result.Status != StockDocumentStatus.Pending) finished++;
                    else logger.LogWarning("Stock document {DocNo} is still pending: {Error}", document.DocNo, result.Error);
                }
                catch (ConcurrencyException)
                {
                    // someone else claimed or changed the document at the same moment: theirs to finish
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Posting of stock document {DocumentId} failed", documentId);
                }
            }
        }
        return finished;
    }
}
