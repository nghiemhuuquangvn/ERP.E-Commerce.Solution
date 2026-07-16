using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Join row: one licensed module in a TenantLicense's module set. A snapshot taken at
    /// issue time from the chosen plan's default set (or a custom override list) —
    /// independently editable afterwards, not a live link back to the plan. Mirrors how
    /// Roles↔Permissions is modeled, not a JSON blob column. Standalone POCO (not BaseEntity).
    /// </summary>
    public class TenantLicenseModule
    {
        public Guid Id { get; private set; }
        public Guid TenantLicenseId { get; private set; }
        public string ModuleCode { get; private set; } = default!;

        private TenantLicenseModule() { }

        public static TenantLicenseModule Create(Guid tenantLicenseId, string moduleCode)
        {
            if (tenantLicenseId == Guid.Empty)
                throw new ArgumentException("TenantLicenseId is required", nameof(tenantLicenseId));
            if (string.IsNullOrWhiteSpace(moduleCode))
                throw new ArgumentException("Module code is required", nameof(moduleCode));

            return new TenantLicenseModule
            {
                Id = Guid.NewGuid(),
                TenantLicenseId = tenantLicenseId,
                ModuleCode = moduleCode.ToUpperInvariant()
            };
        }
    }
}
