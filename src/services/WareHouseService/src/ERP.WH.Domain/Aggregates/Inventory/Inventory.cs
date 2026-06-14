namespace ERP.WH.Domain.Aggregates.Inventory;

public class Inventory
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public int TotalQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int LockedQuantity { get; private set; }
    public int AvailableQuantity => TotalQuantity - ReservedQuantity - LockedQuantity;
    public int ReorderPoint { get; private set; }
    public int ReorderQuantity { get; private set; }
    public decimal Cost { get; private set; }
    public DateTime LastMovementDate { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Inventory(Guid productId, string productName, int reorderPoint = 100, int reorderQuantity = 500)
    {
        ProductId = productId;
        ProductName = productName;
        TotalQuantity = 0;
        ReservedQuantity = 0;
        LockedQuantity = 0;
        ReorderPoint = reorderPoint;
        ReorderQuantity = reorderQuantity;
        Cost = 0;
        LastMovementDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReceiveStock(int quantity, decimal unitCost)
    {
        TotalQuantity += quantity;
        Cost = unitCost;
        LastMovementDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanReserve(int quantity) => AvailableQuantity >= quantity;

    public void ReserveStock(int quantity)
    {
        if (!CanReserve(quantity))
            throw new InvalidOperationException($"Insufficient stock. Available: {AvailableQuantity}, Requested: {quantity}");

        ReservedQuantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseReservation(int quantity)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SellStock(int quantity)
    {
        if (TotalQuantity < quantity)
            throw new InvalidOperationException("Insufficient stock for sale");

        TotalQuantity -= quantity;
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        LastMovementDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AdjustStock(int quantity)
    {
        TotalQuantity += quantity;
        LastMovementDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsLowStock() => TotalQuantity <= ReorderPoint;
}
