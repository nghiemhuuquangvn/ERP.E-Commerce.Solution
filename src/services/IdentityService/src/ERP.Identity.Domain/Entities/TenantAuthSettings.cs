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
        public int SessionTimeoutMinutes { get; private set; } = 15;

        // Session policy (security review 2026-11, SEC-M3; docs/business/Identity/Sessions.md): back-office idle timeout above,
        // absolute limit and the customer pair below; each value at most the platform's (SystemAuthSettings).
        public int SessionAbsoluteHours { get; private set; } = 12;
        public int CustomerSessionTimeoutMinutes { get; private set; } = 10080;
        public int CustomerSessionAbsoluteDays { get; private set; } = 30;

        // Brute-force protection (security review 2026-11, S3; docs/business/Identity/BruteForceProtection.md): MaxLoginAttempts /
        // LockoutDurationMinutes now refuse ONE client IP for one account; these two lock the whole account when failures
        // from every IP together reach the threshold inside the window (and the lock lasts the window).
        public int AccountLockThreshold { get; private set; } = 20;
        public int AccountLockWindowMinutes { get; private set; } = 60;

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
                SessionTimeoutMinutes = 15,
                SessionAbsoluteHours = 12,
                CustomerSessionTimeoutMinutes = 10080,
                CustomerSessionAbsoluteDays = 30,
                AccountLockThreshold = 20,
                AccountLockWindowMinutes = 60,
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

        public void UpdateAccountLockPolicy(int accountLockThreshold, int accountLockWindowMinutes)
        {
            AccountLockThreshold = accountLockThreshold;
            AccountLockWindowMinutes = accountLockWindowMinutes;
        }

        public void UpdateSessionPolicy(int sessionTimeoutMinutes, int sessionAbsoluteHours, int customerSessionTimeoutMinutes, int customerSessionAbsoluteDays)
        {
            SessionTimeoutMinutes = sessionTimeoutMinutes;
            SessionAbsoluteHours = sessionAbsoluteHours;
            CustomerSessionTimeoutMinutes = customerSessionTimeoutMinutes;
            CustomerSessionAbsoluteDays = customerSessionAbsoluteDays;
        }
    }
}
