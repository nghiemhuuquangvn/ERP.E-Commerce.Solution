using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Single well-known-GUID row holding system-wide external-login/2FA toggles.
    /// Mirrors the fixed-GUID convention used by <see cref="Constants.RoleIds"/>.
    /// </summary>
    public class SystemAuthSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("40000000-0000-0000-0000-000000000001");

        public bool GoogleEnabled { get; private set; } = true;
        public bool MicrosoftEnabled { get; private set; } = true;
        public bool FacebookEnabled { get; private set; } = true;
        public bool TwitterEnabled { get; private set; } = true;
        public bool TwoFactorEnabled { get; private set; } = true;

        protected SystemAuthSettings() { }

        public static SystemAuthSettings CreateDefault()
        {
            return new SystemAuthSettings
            {
                Id = WellKnownId,
                GoogleEnabled = true,
                MicrosoftEnabled = true,
                FacebookEnabled = true,
                TwitterEnabled = true,
                TwoFactorEnabled = true,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool googleEnabled, bool microsoftEnabled, bool facebookEnabled, bool twitterEnabled, bool twoFactorEnabled)
        {
            GoogleEnabled = googleEnabled;
            MicrosoftEnabled = microsoftEnabled;
            FacebookEnabled = facebookEnabled;
            TwitterEnabled = twitterEnabled;
            TwoFactorEnabled = twoFactorEnabled;
        }
    }
}
