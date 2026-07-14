using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Single well-known-GUID row holding platform-wide Localization setting toggles.
    /// Deeper i18n config than General Settings' DefaultLanguageCode — number/currency
    /// format locale and RTL support, not the language list itself (owned by
    /// SharedMasterDataService).
    /// </summary>
    public class SystemLocalizationSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("E8000000-0000-0000-0000-000000000001");

        public bool MultiLanguageAllowed { get; private set; } = true;
        public string DefaultLocale { get; private set; } = "en-US";

        protected SystemLocalizationSettings() { }

        public static SystemLocalizationSettings CreateDefault()
        {
            return new SystemLocalizationSettings
            {
                Id = WellKnownId,
                MultiLanguageAllowed = true,
                DefaultLocale = "en-US",
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool multiLanguageAllowed, string defaultLocale)
        {
            MultiLanguageAllowed = multiLanguageAllowed;
            DefaultLocale = defaultLocale;
        }
    }
}
