using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Per-tenant Localization configuration. One row per AuthTenant (TenantId is unique).</summary>
    public class TenantLocalizationSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public string DefaultLocale { get; private set; } = "en-US";
        public string SupportedLocales { get; private set; } = "en-US";
        public string NumberFormatLocale { get; private set; } = "en-US";
        public bool RtlEnabled { get; private set; } = false;

        protected TenantLocalizationSettings() { }

        public static TenantLocalizationSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantLocalizationSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DefaultLocale = "en-US",
                SupportedLocales = "en-US",
                NumberFormatLocale = "en-US",
                RtlEnabled = false,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(string defaultLocale, string supportedLocales, string numberFormatLocale, bool rtlEnabled)
        {
            DefaultLocale = defaultLocale;
            SupportedLocales = supportedLocales;
            NumberFormatLocale = numberFormatLocale;
            RtlEnabled = rtlEnabled;
        }
    }
}
