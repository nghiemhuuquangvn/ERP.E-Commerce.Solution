using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemIntegrationSettingsDto(bool ExternalIntegrationsAllowed);
    public record UpdateSystemIntegrationSettingsRequest(bool ExternalIntegrationsAllowed);

    /// <summary>HasExternalApiKey reports whether a key is stored, never the key itself.</summary>
    public record TenantIntegrationSettingsDto(Guid TenantId, string? ExternalApiBaseUrl, bool HasExternalApiKey, bool SyncEnabled);

    /// <summary>ExternalApiKey is plaintext from the admin form; null/empty means "leave the existing stored key unchanged".</summary>
    public record UpdateTenantIntegrationSettingsRequest(string? ExternalApiBaseUrl, string? ExternalApiKey, bool SyncEnabled);
}
