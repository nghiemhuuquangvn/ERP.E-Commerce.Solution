using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Per-tenant Audit configuration. One row per AuthTenant (TenantId is unique).</summary>
    public class TenantAuditSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public int RetentionDays { get; private set; } = 90;
        public bool LogSuccessfulLogins { get; private set; } = true;
        public bool LogFailedLogins { get; private set; } = true;
        public bool LogPermissionChanges { get; private set; } = true;

        protected TenantAuditSettings() { }

        public static TenantAuditSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantAuditSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RetentionDays = 90,
                LogSuccessfulLogins = true,
                LogFailedLogins = true,
                LogPermissionChanges = true,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(int retentionDays, bool logSuccessfulLogins, bool logFailedLogins, bool logPermissionChanges)
        {
            RetentionDays = retentionDays;
            LogSuccessfulLogins = logSuccessfulLogins;
            LogFailedLogins = logFailedLogins;
            LogPermissionChanges = logPermissionChanges;
        }
    }
}
