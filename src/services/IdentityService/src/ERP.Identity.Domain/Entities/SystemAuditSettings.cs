using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Single well-known-GUID row holding the platform-wide audit-logging switch. Config
    /// only — does not gate the existing SecurityAuditLog/SecurityAuditService yet, and is
    /// deliberately not named AuditLog to avoid confusion with that append-only entity.
    /// </summary>
    public class SystemAuditSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("E6000000-0000-0000-0000-000000000001");

        public bool AuditLoggingEnabled { get; private set; } = true;

        protected SystemAuditSettings() { }

        public static SystemAuditSettings CreateDefault()
        {
            return new SystemAuditSettings
            {
                Id = WellKnownId,
                AuditLoggingEnabled = true,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool auditLoggingEnabled)
        {
            AuditLoggingEnabled = auditLoggingEnabled;
        }
    }
}
