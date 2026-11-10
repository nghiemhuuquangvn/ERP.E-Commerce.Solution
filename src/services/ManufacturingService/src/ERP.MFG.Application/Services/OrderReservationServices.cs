using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.Application.Services;

/// <summary>Has anything happened on the order (BR-PO-09 / BR-PO-13)? Its own figures, and since P3-3 also a stock document that is still on its way to the Warehouse.</summary>
public sealed class OrderTransactionGuard(IStockDocumentRepository documents) : IOrderTransactionGuard
{
    public async Task<bool> HasTransactionsAsync(ProductionOrder order, CancellationToken ct = default)
        => !order.IsUntouched || await documents.CountPendingAsync(order.Id, ct) > 0;
}

/// <summary>Reads the Warehouse for the materials of an order. It has no user (the sync job calls it), so it does not look at plant visibility: callers do.</summary>
public sealed class OrderAvailabilityService(IWarehouseStockClient stock, IPlantClock clock) : IOrderAvailability
{
    public async Task<IReadOnlyDictionary<int, decimal>> AllocatableAsync(ProductionOrder order, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var materials = order.Materials.Where(m => m.OpenNeed > 0 || m.ReservedQty > 0).ToList();
        var answers = await Task.WhenAll(materials.Select(m => stock.GetAvailabilityAsync(m.ComponentItemId, m.WarehouseId, ct)));

        var result = new Dictionary<int, decimal>();
        for (var i = 0; i < materials.Count; i++)
        {
            var warehouse = materials[i].WarehouseId;
            var inWarehouse = answers[i].Balances
                .Where(b => b.WarehouseId == warehouse && (b.LotId == null || (b.LotStatus == "Available" && (b.ExpiryDate is null || b.ExpiryDate > now))))
                .Sum(b => Math.Max(b.AvailableQuantity, 0m));
            result[materials[i].LineNo] = Math.Min(inWarehouse, Math.Max(answers[i].AvailableQuantity, 0m));
        }
        return result;
    }

    public async Task<OrderAvailability> CheckAsync(ProductionOrder order, CancellationToken ct = default)
    {
        var allocatable = await AllocatableAsync(order, ct);
        var rows = order.Materials.Select(m =>
        {
            var plan = ReservationPlanner.Plan(new ReservationNeed(m.LineNo, m.ComponentItemId, m.ComponentCode, m.OpenNeed, allocatable.GetValueOrDefault(m.LineNo), m.ReservedQty));
            return new MaterialAvailability(m.LineNo, m.ComponentItemId, m.ComponentCode, m.ComponentName, m.RequiredQty, plan.Need, plan.Allocatable, plan.Shortage);
        }).ToList();
        return new OrderAvailability(rows);
    }
}

/// <summary>See <see cref="IOrderReservationSync"/>.</summary>
public sealed class OrderReservationSync(IWarehouseStockClient stock, IOrderAvailability availability, IMfgUnitOfWork uow, IPlantClock clock) : IOrderReservationSync
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    /// <summary>15 s, 30 s, 60 s ... up to 5 minutes between two attempts.</summary>
    public static TimeSpan Backoff(int attempts)
        => TimeSpan.FromSeconds(Math.Min(FirstDelay.TotalSeconds * Math.Pow(2, Math.Clamp(attempts, 0, 10)), MaxDelay.TotalSeconds));

    public async Task<ReservationSyncResult> SyncAsync(ProductionOrder order, CancellationToken ct = default)
    {
        try
        {
            await ReconcileAsync(order, ct);
        }
        catch (Exception ex) when (ex is BusinessRuleException or ConcurrencyException)
        {
            // The Warehouse is unreachable (MFG_INT_001), refused (WH_*) or answered a conflict the client could not clear:
            // the order keeps its flag, the reason and the time of the next attempt.
            order.RecordReservationSyncFailure(ex.Message, clock.UtcNow + Backoff(order.ReservationSyncAttempts));
            try { await uow.SaveChangesAsync(ct); } catch (ConcurrencyException) { /* someone else changed the order: the next round settles it */ }
            return new ReservationSyncResult(false, ex.Message);
        }

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyException ex)
        {
            // another request or the job changed the order since it was read; its own sync (idempotent) will settle the reservation
            return new ReservationSyncResult(false, ex.Message);
        }
        return new ReservationSyncResult(true, null);
    }

    private async Task ReconcileAsync(ProductionOrder order, CancellationToken ct)
    {
        var holds = order.Status is OrderStatus.Released or OrderStatus.InProgress or OrderStatus.OnHold;
        var current = await stock.GetReservationAsync(order.Id, ct);
        var open = current is { Status: "Reserved" } ? current : null;

        if (!holds)
        {
            if (open != null) await stock.ReleaseReservationAsync(open.ReservationId, $"Order {order.OrderNo} is {order.Status}", ct);
            order.ApplyReservation(null, new Dictionary<int, decimal>());
            return;
        }

        // what each line should hold: min(need, allocatable + what the order already holds)
        var ownHeld = open?.Lines.Where(l => l.SourceLineId != null).GroupBy(l => l.SourceLineId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Remaining)) ?? [];
        var allocatable = await availability.AllocatableAsync(order, ct);
        var wanted = order.Materials
            .Select(m => (Material: m, Plan: ReservationPlanner.Plan(new ReservationNeed(m.LineNo, m.ComponentItemId, m.ComponentCode, m.OpenNeed, allocatable.GetValueOrDefault(m.LineNo), ownHeld.GetValueOrDefault(m.Id)))))
            .Where(x => x.Plan.Target > 0)
            .ToList();

        if (open != null && Matches(open, wanted))
        {
            order.ApplyReservation(open.ReservationId, wanted.ToDictionary(x => x.Material.LineNo, x => x.Plan.Target));
            return;
        }

        if (open != null) await stock.ReleaseReservationAsync(open.ReservationId, $"Order {order.OrderNo} reservation changed", ct);
        if (wanted.Count == 0)
        {
            order.ApplyReservation(null, new Dictionary<int, decimal>());
            return;
        }

        var warehouse = wanted.Select(x => x.Material.WarehouseId).Distinct().Single();
        var reserved = await stock.ReserveAsync(order.Id, order.OrderNo, warehouse,
            wanted.Select(x => new StockReservationLine(x.Material.ComponentItemId, x.Plan.Target, x.Material.Id)).ToList(), ct);
        order.ApplyReservation(reserved.ReservationId,
            wanted.ToDictionary(x => x.Material.LineNo, x => reserved.Lines.FirstOrDefault(l => l.SourceLineId == x.Material.Id)?.Remaining ?? x.Plan.Target));
    }

    /// <summary>Does the open reservation hold exactly what the plan wants (same lines, same remaining quantities)?</summary>
    private static bool Matches(StockReservation open, IReadOnlyList<(ProductionOrderMaterial Material, ReservationTarget Plan)> wanted)
    {
        var lines = open.Lines.Where(l => l.Remaining > 0).ToList();
        return lines.Count == wanted.Count
            && wanted.All(w => lines.Any(l => l.SourceLineId == w.Material.Id && l.ProductId == w.Material.ComponentItemId && l.Remaining == w.Plan.Target));
    }
}
