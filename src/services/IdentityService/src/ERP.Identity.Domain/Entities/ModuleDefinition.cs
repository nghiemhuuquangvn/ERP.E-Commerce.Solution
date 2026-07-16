using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// System catalog of the 14 licensable backend module codes (everything except
    /// IdentityService, which is the platform itself and is never gated by a license).
    /// Codes match <see cref="ERP.Shared.Authorization.ModuleCodes"/>. Standalone POCO
    /// (not BaseEntity), same shape as SystemFeatureFlag. Seeded once; rarely changes
    /// (only when a new microservice ships).
    /// </summary>
    public class ModuleDefinition
    {
        public Guid Id { get; private set; }
        public string ModuleCode { get; private set; } = default!;
        public string ModuleName { get; private set; } = default!;
        public bool IsActive { get; private set; }

        private ModuleDefinition() { }

        public static ModuleDefinition Create(string moduleCode, string moduleName, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(moduleCode))
                throw new ArgumentException("Module code is required", nameof(moduleCode));
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module name is required", nameof(moduleName));

            return new ModuleDefinition
            {
                Id = Guid.NewGuid(),
                ModuleCode = moduleCode.ToUpperInvariant(),
                ModuleName = moduleName,
                IsActive = isActive
            };
        }

        public void Update(string moduleName, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module name is required", nameof(moduleName));

            ModuleName = moduleName;
            IsActive = isActive;
        }
    }
}
