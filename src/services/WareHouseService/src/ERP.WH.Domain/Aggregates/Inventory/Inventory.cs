using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Inventory;

public class Inventory : ITenantOwned
{
    public Guid ProductId { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string ProductName { get; private set; }
    public string? ProductSku { get; private set; }
    public int TotalQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int LockedQuantity { get; private set; }
    public int AvailableQuantity => TotalQuantity - ReservedQuantity - LockedQuantity;
    public int ReorderPoint { get; private set; }
    public int ReorderQuantity { get; private set; }
    public decimal Cost { get; private set; }
    public Guid? LocationId { get; private set; }
    public ValuationMethod ValuationMethod { get; private set; }
    public DateTime LastMovementDate { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Inventory(Guid productId, string productName, int reorderPoint = 100, int reorderQuantity = 500, string? productSku = null)
    {
        ProductId = productId;
        ProductName = productName;
        ProductSku = productSku;
        TotalQuantity = 0;
        ReservedQuantity = 0;
        LockedQuantity = 0;
        ReorderPoint = reorderPoint;
        ReorderQuantity = reorderQuantity;
        Cost = 0;
        ValuationMethod = ValuationMethod.FIFO;
        LastMovementDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReceiveStock(int quantity, decimal unitCost)
    {
        if (unitCost > 0)
        {
            Cost = ValuationMethod == ValuationMethod.Average && TotalQuantity + quantity > 0
                ? ((Cost * TotalQuantity) + (unitCost * quantity)) / (TotalQuantity + quantity)
                : unitCost;
        }

        TotalQuantity += quantity;
        LastMovementDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignLocation(Guid? locationId)
    {
        LocationId = locationId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetValuationMethod(ValuationMethod method)
    {
        ValuationMethod = method;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetReorderLevels(int reorderPoint, int reorderQuantity)
    {
        ReorderPoint = reorderPoint;
        ReorderQuantity = reorderQuantity;
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

    /// <summary>
    /// A point-of-sale sale already happened physically — the goods left the counter — so
    /// unlike SellStock it never refuses: stock may go negative (signals a count mismatch,
    /// raised as a StockAlert). It doesn't consume online reservations either.
    /// </summary>
    public void RecordPointOfSaleSale(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        TotalQuantity -= quantity;
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
