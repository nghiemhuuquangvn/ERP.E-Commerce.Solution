using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Per-tenant Scheduler configuration. One row per AuthTenant (TenantId is unique).</summary>
    public class TenantSchedulerSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public int OutboxPollingIntervalSeconds { get; private set; } = 30;
        public int MaxRetryAttempts { get; private set; } = 5;

        protected TenantSchedulerSettings() { }

        public static TenantSchedulerSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantSchedulerSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OutboxPollingIntervalSeconds = 30,
                MaxRetryAttempts = 5,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(int outboxPollingIntervalSeconds, int maxRetryAttempts)
        {
            OutboxPollingIntervalSeconds = outboxPollingIntervalSeconds;
            MaxRetryAttempts = maxRetryAttempts;
        }
    }
}
