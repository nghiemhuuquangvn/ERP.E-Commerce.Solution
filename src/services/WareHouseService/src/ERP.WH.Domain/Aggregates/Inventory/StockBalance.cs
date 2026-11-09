using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Aggregates.Inventory;

/// <summary>
/// Stock of one item in one warehouse and (optionally) one lot - the answer to "how much is where".
/// <see cref="Inventory"/> stays the per-item total that Sales, POS and Replenishment read; every write goes through
/// <c>IStockLedger</c> so the two never drift. A row with <see cref="LotId"/> = null holds stock that is not attributed to a lot;
/// <see cref="WarehouseId"/> = null is the "unassigned" bucket of a tenant that has no warehouse at all.
/// </summary>
public class StockBalance : ITenantOwned
{
    public Guid BalanceId { get; private set; }
    /// <summary>Owning tenant - stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal ReservedQuantity { get; private set; }
    public decimal LockedQuantity { get; private set; }
    public decimal AvailableQuantity => Quantity - ReservedQuantity - LockedQuantity;
    public DateTime UpdatedAt { get; private set; }
    /// <summary>Optimistic concurrency token (SQL ROWVERSION): a conflicting write answers HTTP 409.</summary>
    public byte[] RowVersion { get; private set; } = [];

    private StockBalance() { }

    public StockBalance(Guid productId, Guid? warehouseId, Guid? lotId)
    {
        BalanceId = Guid.NewGuid();
        ProductId = productId;
        WarehouseId = warehouseId;
        LotId = lotId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Adds a (possibly negative) quantity. Whether stock may go below zero is the ledger's policy, not the row's.</summary>
    public void Add(decimal quantity)
    {
        Quantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Adds a (possibly negative) reservation; never below zero.</summary>
    public void AddReserved(decimal quantity)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity + quantity);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddLocked(decimal quantity)
    {
        LockedQuantity = Math.Max(0, LockedQuantity + quantity);
        UpdatedAt = DateTime.UtcNow;
    }
}
