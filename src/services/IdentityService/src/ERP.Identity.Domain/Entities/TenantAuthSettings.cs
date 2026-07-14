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

        // User & Security Settings (Phase 2) — tenant's actual configured policy.
        public int MinPasswordLength { get; private set; } = 8;
        public bool RequireUppercase { get; private set; } = true;
        public bool RequireLowercase { get; private set; } = true;
        public bool RequireDigit { get; private set; } = true;
        public bool RequireSymbol { get; private set; } = true;
        public int MaxLoginAttempts { get; private set; } = 5;
        public int LockoutDurationMinutes { get; private set; } = 15;
        public int SessionTimeoutMinutes { get; private set; } = 60;

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
                MinPasswordLength = 8,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireDigit = true,
                RequireSymbol = true,
                MaxLoginAttempts = 5,
                LockoutDurationMinutes = 15,
                SessionTimeoutMinutes = 60,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool googleEnabled, bool microsoftEnabled, bool facebookEnabled, bool twitterEnabled, bool twoFactorEnabled, bool twoFactorRequired,
            int minPasswordLength, bool requireUppercase, bool requireLowercase, bool requireDigit, bool requireSymbol,
            int maxLoginAttempts, int lockoutDurationMinutes, int sessionTimeoutMinutes)
        {
            GoogleEnabled = googleEnabled;
            MicrosoftEnabled = microsoftEnabled;
            FacebookEnabled = facebookEnabled;
            TwitterEnabled = twitterEnabled;
            TwoFactorEnabled = twoFactorEnabled;
            TwoFactorRequired = twoFactorRequired;
            MinPasswordLength = minPasswordLength;
            RequireUppercase = requireUppercase;
            RequireLowercase = requireLowercase;
            RequireDigit = requireDigit;
            RequireSymbol = requireSymbol;
            MaxLoginAttempts = maxLoginAttempts;
            LockoutDurationMinutes = lockoutDurationMinutes;
            SessionTimeoutMinutes = sessionTimeoutMinutes;
        }
    }
}
