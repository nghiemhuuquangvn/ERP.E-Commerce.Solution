using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Per-tenant Advanced (catch-all) configuration. One row per AuthTenant (TenantId is unique).</summary>
    public class TenantAdvancedSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public bool DebugModeEnabled { get; private set; } = false;
        public string? CustomCssUrl { get; private set; }
        public bool DataExportEnabled { get; private set; } = true;

        protected TenantAdvancedSettings() { }

        public static TenantAdvancedSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantAdvancedSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DebugModeEnabled = false,
                DataExportEnabled = true,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool debugModeEnabled, string? customCssUrl, bool dataExportEnabled)
        {
            DebugModeEnabled = debugModeEnabled;
            CustomCssUrl = customCssUrl;
            DataExportEnabled = dataExportEnabled;
        }
    }
}
