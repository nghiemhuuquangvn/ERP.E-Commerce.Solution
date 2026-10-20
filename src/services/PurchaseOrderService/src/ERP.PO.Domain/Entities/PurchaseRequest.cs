using ERP.Shared.Core.Domain;
namespace ERP.PO.Domain.Entities;

public enum PurchaseRequestStatus
{
    Suggested = 0,
    Dismissed = 1,
    Converted = 2
}

/// <summary>
/// A suggestion-only purchase request, auto-created from WareHouseService's
/// StockAlertEvent when a product hits its reorder point — not an aggregate root under
/// BaseEntity/TenantId like PurchaseOrder/Supplier, because the upstream signal itself
/// carries no tenant concept (WareHouseService's Inventory aggregate is not
/// tenant-scoped). A Purchasing user reviews and either dismisses it or converts it,
/// manually, into a real PurchaseOrder via the existing CreatePurchaseOrderCommand —
/// this entity never auto-creates or auto-approves a PurchaseOrder itself.
/// </summary>
public class PurchaseRequest : ITenantOwned
{
    public Guid Id { get; private set; }
    /// <summary>Owning tenant — stamped by PurchasingDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int CurrentQuantity { get; private set; }
    public int ReorderPoint { get; private set; }
    public int SuggestedQuantity { get; private set; }
    public PurchaseRequestStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private PurchaseRequest() { }

    public static PurchaseRequest Create(Guid productId, string productName, int currentQuantity, int reorderPoint, int suggestedQuantity)
    {
        return new PurchaseRequest
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = productName,
            CurrentQuantity = currentQuantity,
            ReorderPoint = reorderPoint,
            SuggestedQuantity = suggestedQuantity,
            Status = PurchaseRequestStatus.Suggested,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Dismiss()
    {
        if (Status != PurchaseRequestStatus.Suggested)
            throw new InvalidOperationException($"Purchase request {Id} is already {Status} and cannot be dismissed.");

        Status = PurchaseRequestStatus.Dismissed;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Convert()
    {
        if (Status != PurchaseRequestStatus.Suggested)
            throw new InvalidOperationException($"Purchase request {Id} is already {Status} and cannot be converted.");

        Status = PurchaseRequestStatus.Converted;
        ResolvedAt = DateTime.UtcNow;
    }
}
