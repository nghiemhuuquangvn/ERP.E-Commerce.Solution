using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// A long-lived credential for service-to-service calls (as opposed to a
    /// short-lived user JWT). Only KeyHash is ever persisted — the raw key is
    /// returned to the caller exactly once, at creation, and cannot be recovered
    /// afterwards. KeyPrefix (first 8 chars of the raw key) is stored in the clear so
    /// ListAsync can show callers which key is which without ever re-exposing the
    /// secret.
    /// </summary>
    public class ApiKey
    {
        public Guid Id { get; private set; }
        public Guid TenantId { get; private set; }
        public string Name { get; private set; } = default!;
        public string KeyPrefix { get; private set; } = default!;
        public string KeyHash { get; private set; } = default!;
        public string? Scopes { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public DateTime? LastUsedAt { get; private set; }
        public string? CreatedBy { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public bool IsValid => IsActive && RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);

        private ApiKey() { }

        public static ApiKey Create(Guid tenantId, string name, string keyPrefix, string keyHash, string? scopes, DateTime? expiresAt, string? createdBy)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required", nameof(name));
            if (string.IsNullOrWhiteSpace(keyHash))
                throw new ArgumentException("KeyHash is required", nameof(keyHash));

            return new ApiKey
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                KeyPrefix = keyPrefix,
                KeyHash = keyHash,
                Scopes = scopes,
                IsActive = true,
                ExpiresAt = expiresAt,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Revoke()
        {
            IsActive = false;
            RevokedAt = DateTime.UtcNow;
        }

        public void Touch()
        {
            LastUsedAt = DateTime.UtcNow;
        }
    }
}
