using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Single well-known-GUID row holding platform-wide API setting toggles. Config only —
    /// does not re-model the existing ApiKey entity/ApiKeysController, and does not yet gate
    /// real rate limiting (Ocelot Gateway's own RateLimitOptions is untouched).
    /// </summary>
    public class SystemApiSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("E5000000-0000-0000-0000-000000000001");

        public bool ApiKeysAllowed { get; private set; } = true;
        public int DefaultRateLimitPerMinute { get; private set; } = 60;

        protected SystemApiSettings() { }

        public static SystemApiSettings CreateDefault()
        {
            return new SystemApiSettings
            {
                Id = WellKnownId,
                ApiKeysAllowed = true,
                DefaultRateLimitPerMinute = 60,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool apiKeysAllowed, int defaultRateLimitPerMinute)
        {
            ApiKeysAllowed = apiKeysAllowed;
            DefaultRateLimitPerMinute = defaultRateLimitPerMinute;
        }
    }
}
