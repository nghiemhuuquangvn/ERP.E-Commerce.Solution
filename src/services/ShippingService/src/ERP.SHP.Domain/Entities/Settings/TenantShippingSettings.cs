using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities.Settings;

/// <summary>Per-tenant Shipping configuration. One row per TenantId (unique).</summary>
public class TenantShippingSettings : BaseEntity
{
    public decimal DefaultShippingFee { get; private set; } = 0m;
    public decimal FreeShippingThreshold { get; private set; } = 0m;

    // P1-11: where the carriers collect the parcels (the shop's pickup address) and the weight assumed for a product that has none.
    public string? SenderName { get; private set; }
    public string? SenderPhone { get; private set; }
    public string? SenderAddress { get; private set; }
    public string? SenderWard { get; private set; }
    public string? SenderDistrict { get; private set; }
    public string? SenderProvince { get; private set; }
    public int DefaultItemWeightGrams { get; private set; } = 500;

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

    public void UpdateSender(string? name, string? phone, string? address, string? ward, string? district, string? province, int defaultItemWeightGrams)
    {
        if (defaultItemWeightGrams <= 0) throw new ArgumentException("The default item weight must be above zero.", nameof(defaultItemWeightGrams));
        SenderName = name?.Trim();
        SenderPhone = phone?.Trim();
        SenderAddress = address?.Trim();
        SenderWard = ward?.Trim();
        SenderDistrict = district?.Trim();
        SenderProvince = province?.Trim();
        DefaultItemWeightGrams = defaultItemWeightGrams;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>The carriers need at least a name, a phone, a street, a district and a province to collect the parcels.</summary>
    public bool HasSender =>
        !string.IsNullOrWhiteSpace(SenderName) && !string.IsNullOrWhiteSpace(SenderPhone) && !string.IsNullOrWhiteSpace(SenderAddress)
        && !string.IsNullOrWhiteSpace(SenderDistrict) && !string.IsNullOrWhiteSpace(SenderProvince);
}
