using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Inventory;

public class Inventory : ITenantOwned
{
    public Guid ProductId { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string ProductName { get; private set; }
    public string? ProductSku { get; private set; }
    public decimal TotalQuantity { get; private set; }
    public decimal ReservedQuantity { get; private set; }
    public decimal LockedQuantity { get; private set; }
    public decimal AvailableQuantity => TotalQuantity - ReservedQuantity - LockedQuantity;
    public decimal ReorderPoint { get; private set; }
    public decimal ReorderQuantity { get; private set; }
    /// <summary>Replenishment policy: order when available stock (plus goods on order) is at or below MinStock, up to MaxStock. ReorderPoint mirrors MinStock for older readers.</summary>
    public decimal MinStock { get; private set; }
    public decimal MaxStock { get; private set; }
    /// <summary>Part of MinStock kept for demand and delivery surprises; shown to the buyer, never ordered separately.</summary>
    public decimal SafetyStock { get; private set; }
    /// <summary>When a low-stock alert was last sent; null while stock is above MinStock, so a movement does not repeat the alert.</summary>
    public DateTime? LowStockAlertedAt { get; private set; }
    public decimal Cost { get; private set; }
    public Guid? LocationId { get; private set; }
    public ValuationMethod ValuationMethod { get; private set; }
    public DateTime LastMovementDate { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    /// <summary>Optimistic concurrency token (SQL ROWVERSION): a conflicting write answers HTTP 409.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public Inventory(Guid productId, string productName, decimal reorderPoint = 100, decimal reorderQuantity = 500, string? productSku = null)
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

    public void ReceiveStock(decimal quantity, decimal unitCost)
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

    public void SetReorderLevels(decimal reorderPoint, decimal reorderQuantity) =>
        SetStockPolicy(reorderPoint, reorderPoint + reorderQuantity, Math.Min(SafetyStock, reorderPoint));

    /// <summary>0 ≤ safety ≤ min &lt; max. ReorderPoint / ReorderQuantity follow, so every older reader sees the same numbers.</summary>
    public void SetStockPolicy(decimal minStock, decimal maxStock, decimal safetyStock)
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

    public bool CanReserve(decimal quantity) => AvailableQuantity >= quantity;

    public void ReserveStock(decimal quantity)
    {
        if (!CanReserve(quantity))
            throw new InvalidOperationException($"Insufficient stock. Available: {AvailableQuantity}, Requested: {quantity}");

        ReservedQuantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseReservation(decimal quantity)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SellStock(decimal quantity)
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
    public void RecordPointOfSaleSale(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        TotalQuantity -= quantity;
        LastMovementDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AdjustStock(decimal quantity)
    {
        TotalQuantity += quantity;
        LastMovementDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Available (not total) stock: goods reserved for customers cannot be sold again.</summary>
    public bool IsLowStock() => AvailableQuantity <= MinStock;

    /// <summary>
    /// The event to send after a stock change, or null. One alert per time below the minimum: the mark is set when the alert is
    /// returned. When stock is back above the minimum the mark is cleared and the same event is returned once more (its available
    /// quantity is then above the minimum), so Purchasing can close a request that is no longer needed.
    /// <paramref name="repeatAfter"/> lets the daily scan (or a policy change, with zero) send the alert again.
    /// </summary>
    public ERP.WH.Domain.Events.StockAlertEvent? EvaluateLowStockAlert(DateTime now, TimeSpan? repeatAfter = null)
    {
        var low = IsLowStock();
        if (!low && LowStockAlertedAt == null) return null;
        if (low && LowStockAlertedAt != null && (repeatAfter == null || now - LowStockAlertedAt < repeatAfter)) return null;

        LowStockAlertedAt = low ? now : null;
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
