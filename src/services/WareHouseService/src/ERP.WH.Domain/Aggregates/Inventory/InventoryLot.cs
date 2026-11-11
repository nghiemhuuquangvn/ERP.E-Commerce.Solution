using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Inventory;

public class InventoryLot : ITenantOwned
{
    public Guid LotId { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string LotNumber { get; private set; } = null!;
    public decimal ReceivedQuantity { get; private set; }
    public decimal RemainingQuantity { get; private set; }
    public decimal ReservedQuantity { get; private set; }
    public decimal AvailableQuantity => RemainingQuantity - ReservedQuantity;
    public decimal UnitCost { get; private set; }
    public DateTime ReceivedDate { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    /// <summary>Warehouse the lot sits in; null only for a tenant without any warehouse ("unassigned").</summary>
    public Guid? WarehouseId { get; private set; }
    public LotStatus Status { get; private set; } = LotStatus.Available;
    public DateTime? ManufactureDate { get; private set; }
    public LotSource Source { get; private set; } = LotSource.Receipt;
    /// <summary>The lot this one was split off from (W7: a partial move to another warehouse creates a sub-lot); null for a lot that came from a receipt.</summary>
    public Guid? ParentLotId { get; private set; }
    /// <summary>Optimistic concurrency token (SQL ROWVERSION): a conflicting write answers HTTP 409.</summary>
    public byte[] RowVersion { get; private set; } = [];

    // EF Core cannot bind a constructor parameter named "quantity" to either
    // ReceivedQuantity or RemainingQuantity by convention (ambiguous), so materialization
    // needs this parameterless constructor + private-setter reflection instead.
    private InventoryLot() { }

    public InventoryLot(
        Guid productId,
        string lotNumber,
        decimal quantity,
        decimal unitCost,
        DateTime? expiryDate = null,
        Guid? purchaseOrderId = null,
        Guid? warehouseId = null,
        DateTime? manufactureDate = null,
        LotSource source = LotSource.Receipt)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Lot quantity must be positive");

        LotId = Guid.NewGuid();
        ProductId = productId;
        LotNumber = lotNumber;
        ReceivedQuantity = quantity;
        RemainingQuantity = quantity;
        UnitCost = unitCost;
        ReceivedDate = DateTime.UtcNow;
        ExpiryDate = expiryDate;
        PurchaseOrderId = purchaseOrderId;
        WarehouseId = warehouseId;
        ManufactureDate = manufactureDate;
        Source = source;
    }

    public bool IsExpired() => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;

    public bool IsDepleted() => RemainingQuantity <= 0;

    /// <summary>Only an Available lot may be allocated or issued; QcHold waits for quality control, Blocked is withdrawn.</summary>
    public bool IsUsable() => Status == LotStatus.Available;

    public void SetStatus(LotStatus status) => Status = status;

    public void AssignWarehouse(Guid? warehouseId) => WarehouseId = warehouseId;

    /// <summary>
    /// W7 lot split: takes <paramref name="quantity"/> out of this lot into a NEW lot that sits in <paramref name="warehouseId"/>. The new lot keeps the
    /// product, unit cost, expiry, manufacture date, purchase order, source, status (a held lot's piece stays held) and received date, and points back
    /// here through <see cref="ParentLotId"/>. This lot loses the quantity from both Remaining and Received, so the tree still adds up to the original
    /// receipt. The caller (<c>StockLedger</c>, the only code that changes stock) has checked 0 &lt; quantity &lt;= available quantity: nothing is refused here.
    /// </summary>
    public InventoryLot SplitOff(decimal quantity, Guid? warehouseId, string number)
    {
        RemainingQuantity -= quantity;
        ReceivedQuantity -= quantity;

        var sub = new InventoryLot(ProductId, number, quantity, UnitCost, ExpiryDate, PurchaseOrderId, warehouseId, ManufactureDate, Source)
        {
            ParentLotId = LotId,
            Status = Status,
            ReceivedDate = ReceivedDate
        };
        return sub;
    }

    /// <summary>
    /// Direct, destructive depletion — used only by the manual "Sold" stock-movement
    /// path (RecordStockMovementCommandHandler), which has no prior reservation to
    /// consume from. Do NOT call this from the reservation flow; use Reserve/Consume
    /// instead so a reservation is a soft hold, not an immediate write-off.
    /// </summary>
    public void Deplete(decimal quantity)
    {
        if (quantity > RemainingQuantity)
            throw new InvalidOperationException($"Cannot deplete {quantity} from lot {LotNumber}; only {RemainingQuantity} remaining");

        RemainingQuantity -= quantity;
    }

    public void Replenish(decimal quantity)
    {
        RemainingQuantity += quantity;
    }

    /// <summary>Soft hold at reservation time — does not touch RemainingQuantity.</summary>
    public void Reserve(decimal quantity)
    {
        if (quantity > AvailableQuantity)
            throw new InvalidOperationException($"Cannot reserve {quantity} from lot {LotNumber}; only {AvailableQuantity} available");

        ReservedQuantity += quantity;
    }

    /// <summary>Releases a soft hold (cancel/expire) — does not touch RemainingQuantity.</summary>
    public void ReleaseReservation(decimal quantity)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
    }

    /// <summary>
    /// Real depletion of a previously soft-reserved quantity, at ship/sell time —
    /// releases the hold and writes off the stock in one step.
    /// </summary>
    public void Consume(decimal quantity)
    {
        if (quantity > RemainingQuantity)
            throw new InvalidOperationException($"Cannot consume {quantity} from lot {LotNumber}; only {RemainingQuantity} remaining");

        RemainingQuantity -= quantity;
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
    }
}
