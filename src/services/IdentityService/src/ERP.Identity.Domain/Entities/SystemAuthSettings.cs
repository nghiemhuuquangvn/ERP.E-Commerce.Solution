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

        // User & Security Settings (Phase 2) — platform-wide baseline.
        // MaxLoginAttempts/LockoutDurationMinutes are read by AuthService.LoginAsync via
        // TenantAuthSettings (fixed 2026-07-21 — previously User.RecordLoginFail()
        // hardcoded a permanent 5-attempt lock with no auto-expiry). PasswordPolicyService
        // still reads PasswordPolicy:* from appsettings.json rather than these fields —
        // that part remains a follow-up.
        public int MinPasswordLength { get; private set; } = 8;
        public bool RequireUppercase { get; private set; } = true;
        public bool RequireLowercase { get; private set; } = true;
        public bool RequireDigit { get; private set; } = true;
        public bool RequireSymbol { get; private set; } = true;
        public int MaxLoginAttempts { get; private set; } = 5;
        public int LockoutDurationMinutes { get; private set; } = 15;
        public int SessionTimeoutMinutes { get; private set; } = 15;

        // Session policy (security review 2026-11, SEC-M3; docs/business/Identity/Sessions.md). SessionTimeoutMinutes is the
        // back-office idle timeout; this platform row is the ceiling of every tenant row.
        public int SessionAbsoluteHours { get; private set; } = 12;
        public int CustomerSessionTimeoutMinutes { get; private set; } = 10080;
        public int CustomerSessionAbsoluteDays { get; private set; } = 30;

        // Brute-force protection (security review 2026-11, S3; docs/business/Identity/BruteForceProtection.md): MaxLoginAttempts /
        // LockoutDurationMinutes now refuse ONE client IP for one account; these two lock the whole account when failures
        // from every IP together reach the threshold inside the window (and the lock lasts the window).
        public int AccountLockThreshold { get; private set; } = 20;
        public int AccountLockWindowMinutes { get; private set; } = 60;

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

        public void Update(bool googleEnabled, bool microsoftEnabled, bool facebookEnabled, bool twitterEnabled, bool twoFactorEnabled,
            int minPasswordLength, bool requireUppercase, bool requireLowercase, bool requireDigit, bool requireSymbol,
            int maxLoginAttempts, int lockoutDurationMinutes, int sessionTimeoutMinutes)
        {
            GoogleEnabled = googleEnabled;
            MicrosoftEnabled = microsoftEnabled;
            FacebookEnabled = facebookEnabled;
            TwitterEnabled = twitterEnabled;
            TwoFactorEnabled = twoFactorEnabled;
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
