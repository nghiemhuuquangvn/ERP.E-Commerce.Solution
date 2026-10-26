using ERP.Shared.Core.Domain;
namespace ERP.PO.Domain.Entities;

public enum PurchaseRequestStatus
{
    Suggested = 0,
    Dismissed = 1,
    Converted = 2,
    /// <summary>A draft purchase order was created from it (P1-9).</summary>
    Ordered = 3,
    /// <summary>An RFQ was created from it (P1-9).</summary>
    Quoting = 4
}

/// <summary>
/// The Purchasing side of a replenishment need, created from WareHouseService's StockAlertEvent when a product's
/// available stock falls to its minimum. It is a proposal: the quantity brings available stock plus goods already on
/// order up to the maximum (<see cref="ReplenishmentCalculator"/>), and a person turns it into a draft purchase order
/// (the product's preferred supplier) or an RFQ, or dismisses it. Nothing is ordered or approved automatically.
/// </summary>
public class PurchaseRequest : ITenantOwned
{
    public Guid Id { get; private set; }
    /// <summary>Owning tenant — stamped by PurchasingDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    /// <summary>The warehouse SKU, taken as the product code of purchase orders and RFQs (documented assumption, DEFERRED R2).</summary>
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    /// <summary>Total quantity when the alert was raised (older rows) — see <see cref="AvailableQuantity"/> for the figure used now.</summary>
    public int CurrentQuantity { get; private set; }
    public int ReorderPoint { get; private set; }
    public int SuggestedQuantity { get; private set; }
    public PurchaseRequestStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public int AvailableQuantity { get; private set; }
    /// <summary>Open quantity (ordered − received) on this product's purchase orders that are not finished or cancelled.</summary>
    public int OnOrderQuantity { get; private set; }
    public int MinStock { get; private set; }
    public int MaxStock { get; private set; }
    public int SafetyStock { get; private set; }
    public Guid? PreferredSupplierId { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public int? LeadTimeDays { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public Guid? RfqId { get; private set; }
    /// <summary>Why it was dismissed (by a person, or "covered by orders" when the need disappeared).</summary>
    public string? Note { get; private set; }

    private PurchaseRequest() { }

    /// <summary>Open = still waiting for a decision.</summary>
    public bool IsOpen => Status == PurchaseRequestStatus.Suggested;

    public static PurchaseRequest Create(Guid productId, string productName, int currentQuantity, int reorderPoint, int suggestedQuantity)
    {
        return new PurchaseRequest
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductCode = productId.ToString("N"),
            ProductName = productName,
            CurrentQuantity = currentQuantity,
            AvailableQuantity = currentQuantity,
            ReorderPoint = reorderPoint,
            MinStock = reorderPoint,
            SuggestedQuantity = suggestedQuantity,
            Status = PurchaseRequestStatus.Suggested,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Creates the request from the figures of a stock alert plus what Purchasing knows (goods on order, preferred supplier).</summary>
    public static PurchaseRequest CreateFromReplenishment(Guid productId, string productCode, string productName, ReplenishmentNeed need)
    {
        var request = Create(productId, productName, need.Available, need.Min, need.SuggestedQuantity);
        request.ProductCode = productCode;
        request.Refresh(need);
        return request;
    }

    /// <summary>A newer alert or scan for the same open request: figures are updated, never a second row.</summary>
    public void Refresh(ReplenishmentNeed need)
    {
        if (!IsOpen) throw new InvalidOperationException($"Purchase request {Id} is {Status} and cannot be refreshed.");

        CurrentQuantity = need.Available;
        AvailableQuantity = need.Available;
        OnOrderQuantity = need.OnOrder;
        ReorderPoint = need.Min;
        MinStock = need.Min;
        MaxStock = need.Max;
        SafetyStock = need.Safety;
        SuggestedQuantity = need.SuggestedQuantity;
    }

    public void SetPreferredSupplier(Guid? supplierId, decimal? unitPrice, int? leadTimeDays)
    {
        if (!IsOpen) return;
        PreferredSupplierId = supplierId;
        UnitPrice = unitPrice;
        LeadTimeDays = leadTimeDays;
    }

    public void Dismiss(string? note = null)
    {
        if (Status != PurchaseRequestStatus.Suggested)
            throw new InvalidOperationException($"Purchase request {Id} is already {Status} and cannot be dismissed.");

        Status = PurchaseRequestStatus.Dismissed;
        Note = note;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Convert()
    {
        if (Status != PurchaseRequestStatus.Suggested)
            throw new InvalidOperationException($"Purchase request {Id} is already {Status} and cannot be converted.");

        Status = PurchaseRequestStatus.Converted;
        ResolvedAt = DateTime.UtcNow;
    }

    public void LinkPurchaseOrder(Guid purchaseOrderId)
    {
        if (!IsOpen) throw new InvalidOperationException($"Purchase request {Id} is already {Status} and cannot be ordered.");

        PurchaseOrderId = purchaseOrderId;
        Status = PurchaseRequestStatus.Ordered;
        ResolvedAt = DateTime.UtcNow;
    }

    public void LinkRfq(Guid rfqId)
    {
        if (!IsOpen) throw new InvalidOperationException($"Purchase request {Id} is already {Status} and cannot be quoted.");

        RfqId = rfqId;
        Status = PurchaseRequestStatus.Quoting;
        ResolvedAt = DateTime.UtcNow;
    }
}

/// <summary>The figures a replenishment decision is made on.</summary>
public record ReplenishmentNeed(int Available, int OnOrder, int Min, int Max, int Safety)
{
    /// <summary>Stock the warehouse has or has coming.</summary>
    public int Covered => Available + OnOrder;

    /// <summary>A request is needed when available stock plus goods on order is at or below the minimum.</summary>
    public bool IsNeeded => Covered <= Min;

    /// <summary>Quantity that brings covered stock up to the maximum; 0 when nothing is needed.</summary>
    public int SuggestedQuantity => IsNeeded ? Math.Max(0, Max - Covered) : 0;
}

public static class ReplenishmentCalculator
{
    /// <summary>
    /// The need for one product. An alert from an older Warehouse (no max) carries only the reorder point and quantity:
    /// then Min = reorder point and Max = reorder point + reorder quantity, and the total quantity stands for the available one.
    /// </summary>
    public static ReplenishmentNeed From(int available, int onOrder, int min, int max, int safety, int reorderPoint, int reorderQuantity) =>
        max > 0
            ? new ReplenishmentNeed(available, onOrder, min, max, safety)
            : new ReplenishmentNeed(available, onOrder, reorderPoint, reorderPoint + reorderQuantity, 0);
}
