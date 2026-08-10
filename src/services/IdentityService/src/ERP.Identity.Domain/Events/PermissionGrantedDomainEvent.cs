using ERP.Identity.Domain.Common;
using System;
using System.Collections.Generic;

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
        public string RoleCode { get; }
        public DateTime OccurredOn { get; }

        // Null when granted by the system rather than by another user (mirrors
        // RoleAssignedDomainEvent's ChangedBy* fields — see that class for why).
        public Guid? ChangedByUserId { get; }
        public string? ChangedByName { get; }

        // This event is raised on Role, not User — every user holding RoleId is
        // affected. The fan-out list is resolved here (by RoleService, which already
        // owns the UserRoles join) rather than by NotificationService, so the event
        // stays fully self-describing.
        public IReadOnlyCollection<Guid> AffectedUserIds { get; }

        public PermissionGrantedDomainEvent(
            Guid roleId,
            Guid tenantId,
            Guid permissionId,
            string permissionCode,
            string roleCode,
            IReadOnlyCollection<Guid>? affectedUserIds = null,
            Guid? changedByUserId = null,
            string? changedByName = null)
        {
            RoleId = roleId;
            TenantId = tenantId;
            PermissionId = permissionId;
            PermissionCode = permissionCode;
            RoleCode = roleCode;
            OccurredOn = DateTime.UtcNow;
            AffectedUserIds = affectedUserIds ?? Array.Empty<Guid>();
            ChangedByUserId = changedByUserId;
            ChangedByName = changedByName;
        }
    }
}
