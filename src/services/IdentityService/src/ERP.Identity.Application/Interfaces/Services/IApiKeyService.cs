using ERP.Identity.Application.DTOs.Security;

namespace ERP.Identity.Application.Interfaces.Services
{
    /// <summary>
    /// Management (create/list/revoke) for service-to-service API keys — an
    /// alternative credential to a short-lived user JWT. Wiring individual services'
    /// gateway routes to accept an API key instead of/alongside a Bearer token is a
    /// separate, per-service integration step; this interface only covers issuing,
    /// validating, and revoking the keys themselves.
    /// </summary>
    public interface IApiKeyService
    {
        Task<ApiKeyCreatedDto> CreateAsync(Guid tenantId, string name, string? scopes, int? expiresInDays, string? createdBy, CancellationToken ct);
        Task<IReadOnlyList<ApiKeyDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
        Task RevokeAsync(Guid id, CancellationToken ct);

        /// <summary>Validates a raw API key presented by a caller; returns the tenant/scopes if valid, null otherwise. Updates LastUsedAt on success.</summary>
        Task<ApiKeyDto?> ValidateAsync(string rawKey, CancellationToken ct);
    }
}
