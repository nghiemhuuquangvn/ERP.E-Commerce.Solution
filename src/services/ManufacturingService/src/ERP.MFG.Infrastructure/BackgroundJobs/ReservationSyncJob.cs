using ERP.MFG.Application.Abstractions;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.MFG.Infrastructure.BackgroundJobs;

/// <summary>
/// Finishes what a request could not: every order whose Warehouse reservation is still to be brought in line (<c>ReservationSyncPending</c>, next attempt due).
/// No user here: the orders are found across tenants, then each one is handled inside its own tenant scope and DI scope, so the service token and the data
/// filters speak for that tenant. The calls are idempotent, so a run that overlaps a request or another instance is harmless. It never gives up: the flag
/// and the stored reason are the alarm.
/// </summary>
public sealed class ReservationSyncJob(IServiceProvider services, ILogger<ReservationSyncJob> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private const int BatchSize = 50;

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
                logger.LogError(ex, "Error in the reservation sync job");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>One round; returns how many orders were brought in line.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        List<(Guid TenantId, Guid OrderId)> pending;
        using (var scope = services.CreateScope())
            pending = await scope.ServiceProvider.GetRequiredService<IProductionOrderRepository>().ListPendingReservationSyncAsync(DateTime.UtcNow, BatchSize, ct);

        var done = 0;
        foreach (var (tenantId, orderId) in pending.Where(p => p.TenantId != Guid.Empty))
        {
            using (TenantScope.Begin(tenantId))
            using (var scope = services.CreateScope())
            {
                try
                {
                    var order = await scope.ServiceProvider.GetRequiredService<IProductionOrderRepository>().GetAsync(orderId, ct);
                    if (order is null || !order.ReservationSyncPending) continue;
                    var result = await scope.ServiceProvider.GetRequiredService<IOrderReservationSync>().SyncAsync(order, ct);
                    if (result.InSync) done++;
                    else logger.LogWarning("Reservation of order {Order} is not in line yet: {Error}", order.OrderNo, result.Error);
                }
                catch (ConcurrencyException)
                {
                    // someone changed the order at the same moment: its own sync, or the next round, settles it
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Reservation sync of order {OrderId} failed", orderId);
                }
            }
        }
        return done;
    }
}
