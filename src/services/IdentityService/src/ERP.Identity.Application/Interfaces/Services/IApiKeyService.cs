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

        /// <summary>
        /// Revokes the key only if it belongs to <paramref name="callerTenantId"/>, unless
        /// <paramref name="bypassTenantCheck"/> is true (the caller holds the SuperAdmin
        /// cross-tenant override) — throws <see cref="UnauthorizedAccessException"/> if a
        /// non-override caller targets another tenant's key. Prevents a tenant Admin from
        /// revoking a different tenant's API key by guessing/enumerating its GUID.
        /// </summary>
        Task RevokeAsync(Guid id, Guid callerTenantId, bool bypassTenantCheck, CancellationToken ct);

        /// <summary>
        /// Validates a raw API key presented by a caller; returns the tenant/scopes if
        /// valid, null otherwise. Updates LastUsedAt on success. When
        /// <paramref name="requiredScope"/> is given, the key's comma-separated
        /// Scopes must contain it, or validation fails — callers (e.g. a future
        /// per-service API-key authentication handler) must pass the operation's
        /// required scope; a null/empty requiredScope only checks the key itself is
        /// valid (unscoped/back-compat callers), not that it's authorized for anything
        /// specific.
        /// </summary>
        Task<ApiKeyDto?> ValidateAsync(string rawKey, string? requiredScope, CancellationToken ct);
    }
}
