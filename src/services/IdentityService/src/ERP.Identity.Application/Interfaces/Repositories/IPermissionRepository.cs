using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IPermissionRepository : IGenericRepository<Permission>
    {
        Task<bool> ExistsAsync(string permissionCode, CancellationToken ct);
        Task<IReadOnlyList<string>> GetPermissionAsync(Guid UserId, Guid TenantId, CancellationToken ct);
        Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, IEnumerable<string> permissionCodes);

        /// <summary>
        /// The active users of the tenant that hold a permission - what their token would carry (an active role of the tenant that has it; platform
        /// permissions only in the platform tenant, where the super-admin role holds the whole catalog). NotificationService uses it to find the approvers.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetUserIdsWithPermissionAsync(string permissionCode, Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<Permission>> ListAllAsync(CancellationToken ct);
        Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct);
        Task AssignToRoleAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct);
        Task RemoveFromRoleAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct);
    }
}
