using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Per-tenant API configuration. One row per AuthTenant (TenantId is unique).</summary>
    public class TenantApiSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public int RateLimitPerMinute { get; private set; } = 60;
        public int MaxActiveApiKeys { get; private set; } = 5;

        protected TenantApiSettings() { }

        public static TenantApiSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantApiSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RateLimitPerMinute = 60,
                MaxActiveApiKeys = 5,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(int rateLimitPerMinute, int maxActiveApiKeys)
        {
            RateLimitPerMinute = rateLimitPerMinute;
            MaxActiveApiKeys = maxActiveApiKeys;
        }
    }
}
