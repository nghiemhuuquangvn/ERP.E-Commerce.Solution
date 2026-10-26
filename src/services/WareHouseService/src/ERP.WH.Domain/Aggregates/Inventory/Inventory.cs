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
    /// <summary>Replenishment policy: order when available stock (plus goods on order) is at or below MinStock, up to MaxStock. ReorderPoint mirrors MinStock for older readers.</summary>
    public int MinStock { get; private set; }
    public int MaxStock { get; private set; }
    /// <summary>Part of MinStock kept for demand and delivery surprises; shown to the buyer, never ordered separately.</summary>
    public int SafetyStock { get; private set; }
    /// <summary>When a low-stock alert was last sent; null while stock is above MinStock, so a movement does not repeat the alert.</summary>
    public DateTime? LowStockAlertedAt { get; private set; }
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
        MinStock = reorderPoint;
        MaxStock = reorderPoint + reorderQuantity;
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

    public void SetReorderLevels(int reorderPoint, int reorderQuantity) =>
        SetStockPolicy(reorderPoint, reorderPoint + reorderQuantity, Math.Min(SafetyStock, reorderPoint));

    /// <summary>0 ≤ safety ≤ min &lt; max. ReorderPoint / ReorderQuantity follow, so every older reader sees the same numbers.</summary>
    public void SetStockPolicy(int minStock, int maxStock, int safetyStock)
    {
        if (safetyStock < 0) throw new ArgumentOutOfRangeException(nameof(safetyStock), "Safety stock cannot be negative.");
        if (minStock < safetyStock) throw new ArgumentOutOfRangeException(nameof(minStock), "Minimum stock cannot be below the safety stock.");
        if (maxStock <= minStock) throw new ArgumentOutOfRangeException(nameof(maxStock), "Maximum stock must be above the minimum.");

        MinStock = minStock;
        MaxStock = maxStock;
        SafetyStock = safetyStock;
        ReorderPoint = minStock;
        ReorderQuantity = maxStock - minStock;
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

    /// <summary>Available (not total) stock: goods reserved for customers cannot be sold again.</summary>
    public bool IsLowStock() => AvailableQuantity <= MinStock;

    /// <summary>
    /// The alert to send after a stock change, or null. One alert per time below the minimum: the mark is set when the alert is
    /// returned and cleared once stock is back above the minimum. <paramref name="repeatAfter"/> lets the daily scan send it again.
    /// </summary>
    public ERP.WH.Domain.Events.StockAlertEvent? EvaluateLowStockAlert(DateTime now, TimeSpan? repeatAfter = null)
    {
        if (!IsLowStock())
        {
            LowStockAlertedAt = null;
            return null;
        }

        if (LowStockAlertedAt != null && (repeatAfter == null || now - LowStockAlertedAt < repeatAfter)) return null;

        LowStockAlertedAt = now;
        return new ERP.WH.Domain.Events.StockAlertEvent
        {
            ProductId = ProductId,
            ProductName = ProductName,
            ProductSku = ProductSku,
            CurrentQuantity = TotalQuantity,
            AvailableQuantity = AvailableQuantity,
            ReorderPoint = ReorderPoint,
            ReorderQuantity = ReorderQuantity,
            MinStock = MinStock,
            MaxStock = MaxStock,
            SafetyStock = SafetyStock,
            EventCreatedAt = now
        };
    }
}
