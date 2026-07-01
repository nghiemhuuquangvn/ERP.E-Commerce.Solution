using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Per-tenant override of the system-wide external-login/2FA toggles.
    /// One row per <see cref="AuthTenant"/> (TenantId is unique).
    /// </summary>
    public class TenantAuthSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public bool GoogleEnabled { get; private set; } = true;
        public bool MicrosoftEnabled { get; private set; } = true;
        public bool FacebookEnabled { get; private set; } = true;
        public bool TwitterEnabled { get; private set; } = true;
        public bool TwoFactorEnabled { get; private set; } = true;
        public bool TwoFactorRequired { get; private set; } = false;

        protected TenantAuthSettings() { }

        public static TenantAuthSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantAuthSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GoogleEnabled = true,
                MicrosoftEnabled = true,
                FacebookEnabled = true,
                TwitterEnabled = true,
                TwoFactorEnabled = true,
                TwoFactorRequired = false,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool googleEnabled, bool microsoftEnabled, bool facebookEnabled, bool twitterEnabled, bool twoFactorEnabled, bool twoFactorRequired)
        {
            GoogleEnabled = googleEnabled;
            MicrosoftEnabled = microsoftEnabled;
            FacebookEnabled = facebookEnabled;
            TwitterEnabled = twitterEnabled;
            TwoFactorEnabled = twoFactorEnabled;
            TwoFactorRequired = twoFactorRequired;
        }
    }
}
