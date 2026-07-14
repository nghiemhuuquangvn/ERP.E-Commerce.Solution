using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Per-tenant company profile. One row per AuthTenant (TenantId is unique). No System
    /// tier — a company profile is inherently tenant-specific, there is no platform-wide
    /// default company. Distinct from AuthTenant.TenantName (a short auth-record label,
    /// not a full company profile).
    /// </summary>
    public class TenantCompanySettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public string CompanyName { get; private set; } = string.Empty;
        public string? LegalName { get; private set; }
        public string? TaxCode { get; private set; }
        public string? Address { get; private set; }
        public string? Phone { get; private set; }
        public string? Email { get; private set; }
        public string? LogoUrl { get; private set; }
        public string? Website { get; private set; }

        protected TenantCompanySettings() { }

        public static TenantCompanySettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantCompanySettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyName = string.Empty,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(string companyName, string? legalName, string? taxCode, string? address, string? phone, string? email, string? logoUrl, string? website)
        {
            CompanyName = companyName;
            LegalName = legalName;
            TaxCode = taxCode;
            Address = address;
            Phone = phone;
            Email = email;
            LogoUrl = logoUrl;
            Website = website;
        }
    }
}
