using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities.Settings;

/// <summary>Per-tenant Tax configuration. One row per TenantId (unique).</summary>
public class TenantTaxSettings : BaseEntity
{
    public string? CompanyTaxCode { get; private set; }
    public decimal DefaultTaxRatePercent { get; private set; } = 0m;
    public bool PricesIncludeTax { get; private set; } = true;

    /// <summary>Code of the e-invoice provider (e.g. "sandbox"); empty = e-invoices are off (ROADMAP P1-3).</summary>
    public string? EInvoiceProvider { get; private set; }

    /// <summary>The invoice series printed on the e-invoices (e.g. "C26TAA").</summary>
    public string? EInvoiceSeries { get; private set; }

    /// <summary>An invoice created from an order is issued as an e-invoice at once (to a retail customer); off by default (ROADMAP P1-3b).</summary>
    public bool AutoIssueEInvoice { get; private set; }

    /// <summary>The currency of the books (3-letter code, default VND); a document in another currency is converted at its rate (ROADMAP P1-4).</summary>
    public string BaseCurrencyCode { get; private set; } = "VND";

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

    public void Update(string? companyTaxCode, decimal defaultTaxRatePercent, bool pricesIncludeTax, string? eInvoiceProvider = null, string? eInvoiceSeries = null, bool autoIssueEInvoice = false, string? baseCurrencyCode = null)
    {
        if (!string.IsNullOrWhiteSpace(baseCurrencyCode))
            BaseCurrencyCode = baseCurrencyCode.Trim().ToUpperInvariant();
        AutoIssueEInvoice = autoIssueEInvoice;
        CompanyTaxCode = companyTaxCode;
        DefaultTaxRatePercent = defaultTaxRatePercent;
        PricesIncludeTax = pricesIncludeTax;
        EInvoiceProvider = string.IsNullOrWhiteSpace(eInvoiceProvider) ? null : eInvoiceProvider.Trim().ToLowerInvariant();
        EInvoiceSeries = string.IsNullOrWhiteSpace(eInvoiceSeries) ? null : eInvoiceSeries.Trim().ToUpperInvariant();
    }
}
