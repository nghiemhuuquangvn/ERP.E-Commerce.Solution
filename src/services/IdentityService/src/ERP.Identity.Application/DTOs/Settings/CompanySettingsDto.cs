using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record TenantCompanySettingsDto(Guid TenantId, string CompanyName, string? LegalName, string? TaxCode, string? Address, string? Phone, string? Email, string? LogoUrl, string? Website);
    public record UpdateTenantCompanySettingsRequest(string CompanyName, string? LegalName, string? TaxCode, string? Address, string? Phone, string? Email, string? LogoUrl, string? Website);
}
