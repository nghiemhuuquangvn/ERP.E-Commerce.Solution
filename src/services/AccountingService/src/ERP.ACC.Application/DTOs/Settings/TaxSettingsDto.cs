namespace ERP.ACC.Application.DTOs.Settings;

public record SystemTaxSettingsDto(bool RegionalTaxConfigAllowed);
public record UpdateSystemTaxSettingsRequest(bool RegionalTaxConfigAllowed);

public record TenantTaxSettingsDto(Guid TenantId, string? CompanyTaxCode, decimal DefaultTaxRatePercent, bool PricesIncludeTax, string? EInvoiceProvider = null, string? EInvoiceSeries = null, bool AutoIssueEInvoice = false);
public record UpdateTenantTaxSettingsRequest(string? CompanyTaxCode, decimal DefaultTaxRatePercent, bool PricesIncludeTax, string? EInvoiceProvider = null, string? EInvoiceSeries = null, bool AutoIssueEInvoice = false);
