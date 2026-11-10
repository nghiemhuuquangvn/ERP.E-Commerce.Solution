using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

/// <summary>One material in the availability check (BR-PO-07): what it needs, what the Warehouse can give in the material's warehouse, what is missing.</summary>
public sealed record MaterialAvailability(int LineNo, Guid ComponentItemId, string ComponentCode, string ComponentName, decimal RequiredQty, decimal Need, decimal Allocatable, decimal Shortage);

public sealed record OrderAvailability(IReadOnlyList<MaterialAvailability> Materials)
{
    public bool HasShortage => Materials.Any(m => m.Shortage > 0);
    public IReadOnlyList<MaterialAvailability> Shortages => Materials.Where(m => m.Shortage > 0).ToList();
}

/// <summary>
/// BR-PO-07: how much of each material the Warehouse can hold for the order. "Allocatable" is the quantity of the material's warehouse that a production
/// reservation may take - stock that is not in a lot, plus lots that are Available and not expired - capped by the item's available stock; what the order
/// already holds counts as available to it.
/// </summary>
public interface IOrderAvailability
{
    Task<OrderAvailability> CheckAsync(ProductionOrder order, CancellationToken ct = default);

    /// <summary>Allocatable quantity per material line number (materials with nothing left to reserve are not asked about).</summary>
    Task<IReadOnlyDictionary<int, decimal>> AllocatableAsync(ProductionOrder order, CancellationToken ct = default);
}

/// <summary>The outcome of bringing the Warehouse reservation in line with an order.</summary>
public sealed record ReservationSyncResult(bool InSync, string? Error);

/// <summary>
/// Idempotent and convergent: whatever the Warehouse holds for the order is released, adopted or replaced so that it equals what the order should hold now
/// (BR-PO-08: min(need, available) per material while Released / InProgress / OnHold, nothing otherwise). It saves the order. A failure keeps
/// <see cref="ProductionOrder.ReservationSyncPending"/> with the reason and the next attempt; it is never thrown to the caller.
/// </summary>
public interface IOrderReservationSync
{
    Task<ReservationSyncResult> SyncAsync(ProductionOrder order, CancellationToken ct = default);
}

/// <summary>"Has anything happened on this order that forbids Unrelease / Cancel?" - the order's own figures now, pending stock documents from P3-3.</summary>
public interface IOrderTransactionGuard
{
    Task<bool> HasTransactionsAsync(ProductionOrder order, CancellationToken ct = default);
}
