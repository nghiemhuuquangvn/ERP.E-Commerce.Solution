using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Single well-known-GUID row holding platform-wide General setting toggles.
    /// Mirrors SystemAuthSettings's System/Tenant two-tier pattern.
    /// </summary>
    public class SystemGeneralSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("C0000000-0000-0000-0000-000000000001");

        public bool MaintenanceModeEnabled { get; private set; } = false;
        public string DefaultLanguageCode { get; private set; } = "en";

        protected SystemGeneralSettings() { }

        public static SystemGeneralSettings CreateDefault()
        {
            return new SystemGeneralSettings
            {
                Id = WellKnownId,
                MaintenanceModeEnabled = false,
                DefaultLanguageCode = "en",
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool maintenanceModeEnabled, string defaultLanguageCode)
        {
            MaintenanceModeEnabled = maintenanceModeEnabled;
            DefaultLanguageCode = defaultLanguageCode;
        }
    }
}
