using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities.Settings;

/// <summary>Per-tenant Tax configuration. One row per TenantId (unique).</summary>
public class TenantTaxSettings : BaseEntity
{
    public string? CompanyTaxCode { get; private set; }
    public decimal DefaultTaxRatePercent { get; private set; } = 0m;
    public bool PricesIncludeTax { get; private set; } = true;

    protected TenantTaxSettings() { }

    public static TenantTaxSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantTaxSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DefaultTaxRatePercent = 0m,
            PricesIncludeTax = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string? companyTaxCode, decimal defaultTaxRatePercent, bool pricesIncludeTax)
    {
        CompanyTaxCode = companyTaxCode;
        DefaultTaxRatePercent = defaultTaxRatePercent;
        PricesIncludeTax = pricesIncludeTax;
    }
}
