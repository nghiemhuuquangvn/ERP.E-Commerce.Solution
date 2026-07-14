using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Per-tenant General configuration. One row per AuthTenant (TenantId is unique).</summary>
    public class TenantGeneralSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public string SiteName { get; private set; } = string.Empty;
        public string DefaultLanguageCode { get; private set; } = "en";
        public string TimeZone { get; private set; } = "UTC";
        public string DateFormat { get; private set; } = "yyyy-MM-dd";
        public bool MaintenanceModeEnabled { get; private set; } = false;

        protected TenantGeneralSettings() { }

        public static TenantGeneralSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantGeneralSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SiteName = string.Empty,
                DefaultLanguageCode = "en",
                TimeZone = "UTC",
                DateFormat = "yyyy-MM-dd",
                MaintenanceModeEnabled = false,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(string siteName, string defaultLanguageCode, string timeZone, string dateFormat, bool maintenanceModeEnabled)
        {
            SiteName = siteName;
            DefaultLanguageCode = defaultLanguageCode;
            TimeZone = timeZone;
            DateFormat = dateFormat;
            MaintenanceModeEnabled = maintenanceModeEnabled;
        }
    }
}
