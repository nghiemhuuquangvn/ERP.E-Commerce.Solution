using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemAuthSettingsDto(
        bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled,
        int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSymbol,
        int MaxLoginAttempts, int LockoutDurationMinutes, int SessionTimeoutMinutes,
        int SessionAbsoluteHours = 12, int CustomerSessionTimeoutMinutes = 10080, int CustomerSessionAbsoluteDays = 30,
        int AccountLockThreshold = 20, int AccountLockWindowMinutes = 60);

    public record UpdateSystemAuthSettingsRequest(
        bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled,
        int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSymbol,
        int MaxLoginAttempts, int LockoutDurationMinutes, int SessionTimeoutMinutes,
        int SessionAbsoluteHours = 12, int CustomerSessionTimeoutMinutes = 10080, int CustomerSessionAbsoluteDays = 30,
        int AccountLockThreshold = 20, int AccountLockWindowMinutes = 60,
        // SEC-M6: changing auth settings re-asks the caller's password (or a fresh external sign-in).
        string? CurrentPassword = null);

    public record TenantAuthSettingsDto(
        Guid TenantId, bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled, bool TwoFactorRequired,
        int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSymbol,
        int MaxLoginAttempts, int LockoutDurationMinutes, int SessionTimeoutMinutes,
        int SessionAbsoluteHours = 12, int CustomerSessionTimeoutMinutes = 10080, int CustomerSessionAbsoluteDays = 30,
        int AccountLockThreshold = 20, int AccountLockWindowMinutes = 60);

    public record UpdateTenantAuthSettingsRequest(
        bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled, bool TwoFactorRequired,
        int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSymbol,
        int MaxLoginAttempts, int LockoutDurationMinutes, int SessionTimeoutMinutes,
        int SessionAbsoluteHours = 12, int CustomerSessionTimeoutMinutes = 10080, int CustomerSessionAbsoluteDays = 30,
        int AccountLockThreshold = 20, int AccountLockWindowMinutes = 60,
        // SEC-M6: changing auth settings re-asks the caller's password (or a fresh external sign-in).
        string? CurrentPassword = null);

    public record PublicAuthOptionsDto(bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled);
}
