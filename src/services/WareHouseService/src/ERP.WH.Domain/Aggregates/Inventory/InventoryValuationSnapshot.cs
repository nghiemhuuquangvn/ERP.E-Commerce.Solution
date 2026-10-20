using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Inventory;

public class InventoryValuationSnapshotLine : ITenantOwned
{
    public Guid Id { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalValue { get; private set; }

    private InventoryValuationSnapshotLine() { }

    public InventoryValuationSnapshotLine(Guid snapshotId, Guid productId, int quantity, decimal unitCost)
    {
        Id = Guid.NewGuid();
        SnapshotId = snapshotId;
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
        TotalValue = quantity * unitCost;
    }
}

/// <summary>
/// Period-end inventory valuation, closed once and immutable afterward — the doc's
/// core "chốt kỳ" rule. Additive: does not replace GetInventoryValuationQuery's
/// live, on-demand per-product calculation, which stays as-is for day-to-day reads.
/// </summary>
public class InventoryValuationSnapshot : ITenantOwned
{
    public Guid Id { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string PeriodCode { get; private set; } = null!;
    public Guid? WarehouseId { get; private set; }
    public DateTime SnapshotDate { get; private set; }
    public bool IsClosed { get; private set; }
    public List<InventoryValuationSnapshotLine> Lines { get; private set; } = new();

    private InventoryValuationSnapshot() { }

    public InventoryValuationSnapshot(string periodCode, Guid? warehouseId)
    {
        Id = Guid.NewGuid();
        PeriodCode = periodCode;
        WarehouseId = warehouseId;
        SnapshotDate = DateTime.UtcNow;
        IsClosed = true;
        Lines = new List<InventoryValuationSnapshotLine>();
    }

    public void AddLine(Guid productId, int quantity, decimal unitCost)
    {
        Lines.Add(new InventoryValuationSnapshotLine(Id, productId, quantity, unitCost));
    }

    public decimal TotalValue => Lines.Sum(l => l.TotalValue);
}
