using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Events
{
    /// <summary>
    /// Raised when a permission is granted to a role (every user holding that role
    /// effectively gains it) — matches the "PermissionGranted" event named in
    /// CLAUDE.md's Identity published-events list.
    /// </summary>
    public class PermissionGrantedDomainEvent : IDomainEvent
    {
        public Guid RoleId { get; }
        public Guid TenantId { get; }
        public Guid PermissionId { get; }
        public string PermissionCode { get; }
        public DateTime OccurredOn { get; }

        public PermissionGrantedDomainEvent(Guid roleId, Guid tenantId, Guid permissionId, string permissionCode)
        {
            RoleId = roleId;
            TenantId = tenantId;
            PermissionId = permissionId;
            PermissionCode = permissionCode;
            OccurredOn = DateTime.UtcNow;
        }
    }
}
