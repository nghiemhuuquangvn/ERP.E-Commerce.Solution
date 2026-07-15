using ERP.Identity.Application.DTOs.Security;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System.Security.Cryptography;

namespace ERP.Identity.Infrastructure.Services
{
    public class ApiKeyService : IApiKeyService
    {
        private const string KeyPrefixTag = "erpsk"; // "ERP Service Key" — visually distinguishable from a JWT
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public ApiKeyService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task<ApiKeyCreatedDto> CreateAsync(Guid tenantId, string name, string? scopes, int? expiresInDays, string? createdBy, CancellationToken ct)
        {
            var randomPart = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var rawKey = $"{KeyPrefixTag}_{randomPart}";
            var prefix = rawKey[..12]; // "erpsk_" + 6 hex chars — enough entropy to be practically unique, short enough to display

            var keyHash = _passwordHasher.HashPassword(rawKey);
            var expiresAt = expiresInDays.HasValue ? DateTime.UtcNow.AddDays(expiresInDays.Value) : (DateTime?)null;

            var apiKey = ApiKey.Create(tenantId, name, prefix, keyHash, scopes, expiresAt, createdBy);
            await _unitOfWork.ApiKeys.AddAsync(apiKey, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new ApiKeyCreatedDto(ToDto(apiKey), rawKey);
        }

        public async Task<IReadOnlyList<ApiKeyDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            var keys = await _unitOfWork.ApiKeys.ListByTenantAsync(tenantId, ct);
            return keys.Select(ToDto).ToList();
        }

        public async Task RevokeAsync(Guid id, Guid callerTenantId, bool bypassTenantCheck, CancellationToken ct)
        {
            var key = await _unitOfWork.ApiKeys.GetByIdAsync(id, ct);
            if (key == null)
                throw new InvalidOperationException("API key not found");

            if (!bypassTenantCheck && key.TenantId != callerTenantId)
                throw new UnauthorizedAccessException("API key belongs to a different tenant");

            key.Revoke();
            await _unitOfWork.ApiKeys.UpdateAsync(key, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<ApiKeyDto?> ValidateAsync(string rawKey, string? requiredScope, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rawKey) || rawKey.Length < 12)
                return null;

            var prefix = rawKey[..12];
            var apiKey = await _unitOfWork.ApiKeys.GetByPrefixAsync(prefix, ct);
            if (apiKey == null || !apiKey.IsValid)
                return null;

            if (!_passwordHasher.VerifyPassword(apiKey.KeyHash, rawKey))
                return null;

            if (!string.IsNullOrWhiteSpace(requiredScope))
            {
                var grantedScopes = (apiKey.Scopes ?? string.Empty)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (!grantedScopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase))
                    return null;
            }

            apiKey.Touch();
            await _unitOfWork.ApiKeys.UpdateAsync(apiKey, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ToDto(apiKey);
        }

        private static ApiKeyDto ToDto(ApiKey k) =>
            new(k.Id, k.TenantId, k.Name, k.KeyPrefix, k.Scopes, k.IsActive, k.ExpiresAt, k.LastUsedAt, k.CreatedAt);
    }
}
