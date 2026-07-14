using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// A per-tenant override of a SystemFeatureFlag's effective value, keyed by
    /// (TenantId, Key) — unique together. Absence of a row for a key means the tenant
    /// inherits the System flag's IsEnabled value unmodified.
    /// </summary>
    public class TenantFeatureFlagOverride
    {
        public Guid Id { get; private set; }
        public Guid TenantId { get; private set; }
        public string Key { get; private set; } = default!;
        public bool IsEnabled { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private TenantFeatureFlagOverride() { }

        public static TenantFeatureFlagOverride Create(Guid tenantId, string key, bool isEnabled)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key is required", nameof(key));

            var now = DateTime.UtcNow;
            return new TenantFeatureFlagOverride
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = key,
                IsEnabled = isEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        public void SetEnabled(bool isEnabled)
        {
            IsEnabled = isEnabled;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
