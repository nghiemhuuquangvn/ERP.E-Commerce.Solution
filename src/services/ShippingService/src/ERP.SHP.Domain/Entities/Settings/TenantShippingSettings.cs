using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities.Settings;

/// <summary>Per-tenant Shipping configuration. One row per TenantId (unique).</summary>
public class TenantShippingSettings : BaseEntity
{
    public decimal DefaultShippingFee { get; private set; } = 0m;
    public decimal FreeShippingThreshold { get; private set; } = 0m;

    protected TenantShippingSettings() { }

    public static TenantShippingSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantShippingSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DefaultShippingFee = 0m,
            FreeShippingThreshold = 0m,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(decimal defaultShippingFee, decimal freeShippingThreshold)
    {
        DefaultShippingFee = defaultShippingFee;
        FreeShippingThreshold = freeShippingThreshold;
    }
}
