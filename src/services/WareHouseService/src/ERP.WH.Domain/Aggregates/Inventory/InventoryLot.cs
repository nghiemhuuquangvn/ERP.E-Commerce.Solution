namespace ERP.WH.Domain.Aggregates.Inventory;

public class InventoryLot
{
    public Guid LotId { get; private set; }
    public Guid ProductId { get; private set; }
    public string LotNumber { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public int RemainingQuantity { get; private set; }
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
        int quantity,
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

    public void Deplete(int quantity)
    {
        if (quantity > RemainingQuantity)
            throw new InvalidOperationException($"Cannot deplete {quantity} from lot {LotNumber}; only {RemainingQuantity} remaining");

        RemainingQuantity -= quantity;
    }

    public void Replenish(int quantity)
    {
        RemainingQuantity += quantity;
    }
}
