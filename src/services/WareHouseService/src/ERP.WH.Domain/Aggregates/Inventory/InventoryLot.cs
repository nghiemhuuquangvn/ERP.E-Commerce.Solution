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
        Guid? purchaseOrderId = null)
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
    }

    public bool IsExpired() => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;

    public bool IsDepleted() => RemainingQuantity <= 0;

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
