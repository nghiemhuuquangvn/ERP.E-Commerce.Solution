using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemFileStorageSettingsDto(bool CustomStorageProviderAllowed, int DefaultMaxUploadSizeMb);
    public record UpdateSystemFileStorageSettingsRequest(bool CustomStorageProviderAllowed, int DefaultMaxUploadSizeMb);

    public record TenantFileStorageSettingsDto(Guid TenantId, string Provider, int MaxUploadSizeMb, string? CdnBaseUrl);
    public record UpdateTenantFileStorageSettingsRequest(string Provider, int MaxUploadSizeMb, string? CdnBaseUrl);
}
