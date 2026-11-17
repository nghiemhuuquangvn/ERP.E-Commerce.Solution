using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemAuthSettingsDto(
        bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled,
        int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSymbol,
        int MaxLoginAttempts, int LockoutDurationMinutes, int SessionTimeoutMinutes,
        int SessionAbsoluteHours = 12, int CustomerSessionTimeoutMinutes = 10080, int CustomerSessionAbsoluteDays = 30);

    public record UpdateSystemAuthSettingsRequest(
        bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled,
        int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSymbol,
        int MaxLoginAttempts, int LockoutDurationMinutes, int SessionTimeoutMinutes,
        int SessionAbsoluteHours = 12, int CustomerSessionTimeoutMinutes = 10080, int CustomerSessionAbsoluteDays = 30);

    public record TenantAuthSettingsDto(
        Guid TenantId, bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled, bool TwoFactorRequired,
        int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSymbol,
        int MaxLoginAttempts, int LockoutDurationMinutes, int SessionTimeoutMinutes,
        int SessionAbsoluteHours = 12, int CustomerSessionTimeoutMinutes = 10080, int CustomerSessionAbsoluteDays = 30);

    public record UpdateTenantAuthSettingsRequest(
        bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled, bool TwoFactorRequired,
        int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSymbol,
        int MaxLoginAttempts, int LockoutDurationMinutes, int SessionTimeoutMinutes,
        int SessionAbsoluteHours = 12, int CustomerSessionTimeoutMinutes = 10080, int CustomerSessionAbsoluteDays = 30);

    public record PublicAuthOptionsDto(bool GoogleEnabled, bool MicrosoftEnabled, bool FacebookEnabled, bool TwitterEnabled, bool TwoFactorEnabled);
}
