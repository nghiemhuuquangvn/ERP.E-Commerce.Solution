using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemApiSettingsDto(bool ApiKeysAllowed, int DefaultRateLimitPerMinute);
    public record UpdateSystemApiSettingsRequest(bool ApiKeysAllowed, int DefaultRateLimitPerMinute);

    public record TenantApiSettingsDto(Guid TenantId, int RateLimitPerMinute, int MaxActiveApiKeys);
    public record UpdateTenantApiSettingsRequest(int RateLimitPerMinute, int MaxActiveApiKeys);
}
