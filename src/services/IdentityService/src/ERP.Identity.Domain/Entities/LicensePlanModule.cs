using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Join row: one licensable module included in a LicensePlan's default module set.
    /// Mirrors how Roles↔Permissions is modeled (RolePermission), not a JSON blob column.
    /// Standalone POCO (not BaseEntity).
    /// </summary>
    public class LicensePlanModule
    {
        public Guid Id { get; private set; }
        public Guid LicensePlanId { get; private set; }
        public string ModuleCode { get; private set; } = default!;

        private LicensePlanModule() { }

        public static LicensePlanModule Create(Guid licensePlanId, string moduleCode)
        {
            if (licensePlanId == Guid.Empty)
                throw new ArgumentException("LicensePlanId is required", nameof(licensePlanId));
            if (string.IsNullOrWhiteSpace(moduleCode))
                throw new ArgumentException("Module code is required", nameof(moduleCode));

            return new LicensePlanModule
            {
                Id = Guid.NewGuid(),
                LicensePlanId = licensePlanId,
                ModuleCode = moduleCode.ToUpperInvariant()
            };
        }
    }
}
