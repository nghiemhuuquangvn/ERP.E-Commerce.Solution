using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Entities;

/// <summary>
/// What a Supplier can supply, at what reference price/lead time. ProductId/VariantId
/// reference ProductService's Product/ProductVariant by Id only (cross-service, no
/// database FK — see CLAUDE.md database ownership rules).
/// </summary>
public class SupplierItem : BaseEntity
{
    public Guid SupplierId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? VariantId { get; private set; }
    public string? SupplierItemCode { get; private set; }
    public decimal LastPurchasePrice { get; private set; }
    public int LeadTimeDays { get; private set; }

    private SupplierItem() { }

    public static SupplierItem Create(
        Guid supplierId,
        Guid tenantId,
        Guid productId,
        decimal lastPurchasePrice,
        int leadTimeDays,
        Guid? variantId = null,
        string? supplierItemCode = null)
    {
        if (supplierId == Guid.Empty)
            throw new ArgumentException("Supplier ID is required", nameof(supplierId));

        if (productId == Guid.Empty)
            throw new ArgumentException("Product ID is required", nameof(productId));

        if (lastPurchasePrice < 0)
            throw new ArgumentException("Last purchase price cannot be negative", nameof(lastPurchasePrice));

        if (leadTimeDays < 0)
            throw new ArgumentException("Lead time cannot be negative", nameof(leadTimeDays));

        return new SupplierItem
        {
            SupplierId = supplierId,
            TenantId = tenantId,
            ProductId = productId,
            VariantId = variantId,
            SupplierItemCode = supplierItemCode,
            LastPurchasePrice = lastPurchasePrice,
            LeadTimeDays = leadTimeDays,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(decimal lastPurchasePrice, int leadTimeDays, string? supplierItemCode)
    {
        if (lastPurchasePrice < 0)
            throw new ArgumentException("Last purchase price cannot be negative", nameof(lastPurchasePrice));

        if (leadTimeDays < 0)
            throw new ArgumentException("Lead time cannot be negative", nameof(leadTimeDays));

        LastPurchasePrice = lastPurchasePrice;
        LeadTimeDays = leadTimeDays;
        SupplierItemCode = supplierItemCode;
        UpdatedAt = DateTime.UtcNow;
    }
}
