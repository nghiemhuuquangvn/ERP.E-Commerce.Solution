using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities.Settings;

/// <summary>
/// Per-tenant Store/Product configuration. One row per TenantId (unique).
/// </summary>
public class TenantStoreProductSettings : BaseEntity
{
    public string StoreName { get; private set; } = string.Empty;
    public string? StoreDescription { get; private set; }
    public string? LogoUrl { get; private set; }
    public bool ShowOutOfStockProducts { get; private set; } = true;

    protected TenantStoreProductSettings() { }

    public static TenantStoreProductSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantStoreProductSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StoreName = string.Empty,
            ShowOutOfStockProducts = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string storeName, string? storeDescription, string? logoUrl, bool showOutOfStockProducts)
    {
        StoreName = storeName;
        StoreDescription = storeDescription;
        LogoUrl = logoUrl;
        ShowOutOfStockProducts = showOutOfStockProducts;
    }
}
