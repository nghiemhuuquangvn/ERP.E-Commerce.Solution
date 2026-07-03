using ERP.Identity.Domain.Common;
using ERP.Identity.Domain.Constants;
using ERP.Identity.Domain.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class Role : BaseEntity
    {
        public string RoleCode { get; set; } = default!;
        public string RoleName { get; set; } = default!;
        public Guid TenantId { get; set; }
        public bool IsActive { get; private set; }
        public virtual ICollection<UserRole> UserRoles { get; set; } = default!;
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = default!;
        public Role() { }

        private Role(Guid id, string roleCode, string roleName, Guid tenantId)
        {
            Id = id;
            RoleCode = roleCode;
            RoleName = roleName;
            TenantId = tenantId;
            IsActive = true;
        }
        private Role(Guid id, string roleCode)
        {
            Id = id;
            RoleCode = roleCode;
        }
        private Role(Guid id, string roleCode, Guid tenantId)
        {
            Id = id;
            RoleCode = roleCode;
            TenantId = tenantId;
        }
        public static Role Create(string roleCode, string roleName, Guid tenantId)
        {
            return new Role(Guid.NewGuid(), roleCode, roleName, tenantId);
        }
        public static Role CreateReference(string code)
        {
            return new Role(RoleIds.FromCode(code), code);
        }
        public static Role CreateReference(string code, Guid tenantId)
        {
            return new Role(RoleIds.FromCode(code), code, tenantId);
        }

        /// <summary>
        /// Raises PermissionGranted for a permission just assigned to this role by
        /// IPermissionRepository.AssignToRoleAsync — the join-row insert itself stays in
        /// the repository (RolePermissions is a plain many-to-many, not a rich collection
        /// owned by this aggregate), but the domain event belongs on Role since it's the
        /// side everyone else's users-gain-a-permission reasoning is about.
        /// </summary>
        public void NotifyPermissionGranted(Guid permissionId, string permissionCode)
        {
            AddDomainEvent(new PermissionGrantedDomainEvent(Id, TenantId, permissionId, permissionCode));
        }
    }

}
