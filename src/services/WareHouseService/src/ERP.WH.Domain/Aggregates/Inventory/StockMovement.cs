using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Inventory;

public enum MovementType
{
    Received = 0,
    Sold = 1,
    Adjusted = 2,
    Damaged = 3,
    Transferred = 4,

    /// <summary>Goods of a returned order back on the shelf (ROADMAP P1-5); booked by Accounting as Dr Inventory / Cr Cost of goods sold.</summary>
    Returned = 5,

    /// <summary>Goods sent back to the supplier (ROADMAP P1-8); booked by Accounting as Dr Goods received not invoiced / Cr Inventory.</summary>
    ReturnedToVendor = 6,

    // Production movements (Warehouse upgrade W3). Quantity is always positive; the type gives the direction.
    // Accounting books none of them until the WIP posting of Manufacturing phase 6.
    /// <summary>Material issued to a production order: stock and value go down.</summary>
    ProductionIssue = 7,
    /// <summary>Unused material returned from a production order: stock and value go up at the cost passed.</summary>
    ProductionReturn = 8,
    /// <summary>Finished good received from a production order, optionally into a lot on quality hold.</summary>
    ProductionReceipt = 9,
    /// <summary>Scrap written off during production: stock and value go down.</summary>
    ProductionScrap = 10,
    /// <summary>A lot on hold released for use: only the lot status changes.</summary>
    QcRelease = 11,
    /// <summary>Stock sent to a subcontract warehouse: the balance moves between warehouses, the item total stays.</summary>
    SubcontractTransfer = 12
}

public static class MovementTypes
{
    /// <summary>Types from the production flows (the ones protected by the unique idempotency index).</summary>
    public static bool IsProduction(this MovementType type) => (int)type >= (int)MovementType.ProductionIssue;
}

public class StockMovement : ITenantOwned
{
    public Guid MovementId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public MovementType MovementType { get; private set; }
    public string? FromLocation { get; private set; }
    public string? ToLocation { get; private set; }
    public string? Reference { get; private set; }
    public string? Reason { get; private set; }
    public Guid? SourceOrderId { get; private set; }
    public DateTime RecordedAt { get; private set; }

    // Filled from W3 on; rows older than that keep NULL ("not recorded").
    public Guid? WarehouseId { get; private set; }
    public Guid? LotId { get; private set; }
    public decimal? UnitCost { get; private set; }
    public decimal? ValueDelta { get; private set; }
    /// <summary>Signed effect on the per-item total (Quantity has no consistent sign for the older types).</summary>
    public decimal? StockDelta { get; private set; }

    /// <summary>
    /// The lot(s) the movement acted on (W7). <see cref="LotId"/> stays the lot named in the request (and null for an issue that drew from several
    /// lots); these rows say which lots were really used: one per lot taken by a reservation issue, the lot of a free issue / scrap / return /
    /// quality release / receipt, the new sub-lot of a split transfer. A lot-less remainder is not a row. Movements booked before W7 have no rows.
    /// </summary>
    public List<StockMovementLot> Lots { get; private set; } = [];

    /// <summary>Records one lot the movement acted on.</summary>
    public StockMovement AddLot(Guid lotId, string lotNumber, decimal quantity, decimal unitCost, Guid? warehouseId, Guid? parentLotId = null)
    {
        Lots.Add(new StockMovementLot(MovementId, Lots.Count, lotId, lotNumber, quantity, unitCost, warehouseId, parentLotId));
        return this;
    }

    /// <summary>Where, in which lot, at what cost and with what effect the movement happened.</summary>
    public StockMovement Describe(Guid? warehouseId, Guid? lotId, decimal unitCost, decimal valueDelta, decimal stockDelta)
    {
        WarehouseId = warehouseId;
        LotId = lotId;
        UnitCost = unitCost;
        ValueDelta = valueDelta;
        StockDelta = stockDelta;
        return this;
    }

    public StockMovement(
        Guid tenantId,
        Guid productId,
        decimal quantity,
        MovementType movementType,
        string? fromLocation,
        string? toLocation,
        string? reference,
        string? reason = null,
        Guid? sourceOrderId = null)
    {
        MovementId = Guid.NewGuid();
        TenantId = tenantId;
        ProductId = productId;
        Quantity = quantity;
        MovementType = movementType;
        FromLocation = fromLocation;
        ToLocation = toLocation;
        Reference = reference;
        Reason = reason;
        SourceOrderId = sourceOrderId;
        RecordedAt = DateTime.UtcNow;
    }
}

/// <summary>One lot a stock movement acted on (W7): the per-lot split of a production movement, kept so that traceability can read it later.</summary>
public class StockMovementLot : ITenantOwned
{
    public Guid LotRowId { get; private set; }
    /// <summary>Owning tenant - stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public Guid MovementId { get; private set; }
    /// <summary>Order of the rows in the movement's answer (0, 1, ...): a replay must list the lots in the same order as the first answer.</summary>
    public int Position { get; private set; }
    public Guid LotId { get; private set; }
    /// <summary>The lot number at the time (a snapshot: the answer of a replay must not change if the lot is renamed).</summary>
    public string LotNumber { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    /// <summary>The cost used for this lot's share of the movement value.</summary>
    public decimal? UnitCost { get; private set; }
    public Guid? WarehouseId { get; private set; }
    /// <summary>Set when the lot is a sub-lot created by this movement: the lot it was split off from.</summary>
    public Guid? ParentLotId { get; private set; }

    private StockMovementLot() { }

    public StockMovementLot(Guid movementId, int position, Guid lotId, string lotNumber, decimal quantity, decimal? unitCost, Guid? warehouseId, Guid? parentLotId)
    {
        LotRowId = Guid.NewGuid();
        MovementId = movementId;
        Position = position;
        LotId = lotId;
        LotNumber = lotNumber;
        Quantity = quantity;
        UnitCost = unitCost;
        WarehouseId = warehouseId;
        ParentLotId = parentLotId;
    }
}
