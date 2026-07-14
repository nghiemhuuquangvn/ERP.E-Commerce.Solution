using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities.Settings;

/// <summary>Per-tenant Currency configuration. One row per TenantId (unique).</summary>
public class TenantCurrencySettings : BaseEntity
{
    public string DefaultCurrencyCode { get; private set; } = "USD";
    public string ExchangeRateSource { get; private set; } = "Manual";
    public int DecimalDigits { get; private set; } = 2;

    protected TenantCurrencySettings() { }

    public static TenantCurrencySettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantCurrencySettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DefaultCurrencyCode = "USD",
            ExchangeRateSource = "Manual",
            DecimalDigits = 2,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string defaultCurrencyCode, string exchangeRateSource, int decimalDigits)
    {
        DefaultCurrencyCode = defaultCurrencyCode;
        ExchangeRateSource = exchangeRateSource;
        DecimalDigits = decimalDigits;
    }
}
