using ERP.Identity.Application.Authorization;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class PermissionRepository : GenericRepository<Permission>, IPermissionRepository
    {
        public PermissionRepository(AuthDbContext dbContext) : base(dbContext) { }
        public async Task<IReadOnlyList<string>> GetPermissionAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            var permissions = await (
                    from ur in _dbContext.UserRoles.AsNoTracking()
                    join rp in _dbContext.RolePermissions.AsNoTracking()
                        on ur.RoleId equals rp.RoleId
                    join p in _dbContext.Permissions.AsNoTracking()
                        on rp.PermissionId equals p.Id
                    where ur.UserId == userId
                        && ur.TenantId == tenantId
                    select p.PermissionCode
                )
                .Distinct()
                .ToListAsync(ct);

            return permissions;
        }
        public async Task<bool> ExistsAsync(string permissionCode, CancellationToken ct)
        {
            return await _dbContext.Permissions.AsNoTracking()
                .AnyAsync(p => p.PermissionCode == permissionCode, ct);
        }

        public async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, IEnumerable<string> permissionCodes)
        {
            var userPermissions = await _dbContext.UserRoles.AsNoTracking()
                            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId)
                            .SelectMany(ur => _dbContext.RolePermissions.AsNoTracking()
                                .Where(rp => rp.RoleId == ur.RoleId))
                            .Select(rp => rp.Permission.PermissionCode)
                            .ToListAsync();

            return permissionCodes.Any(code => userPermissions.Contains(code));
        }

        public async Task<IReadOnlyList<Permission>> ListAllAsync(CancellationToken ct)
        {
            return await _dbContext.Permissions.AsNoTracking().ToListAsync(ct);
        }

        public async Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct)
        {
            return await _dbContext.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.Permission.PermissionCode)
                .ToListAsync(ct);
        }

        public async Task AssignToRoleAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct)
        {
            var existing = await _dbContext.RolePermissions.FirstOrDefaultAsync(
                rp => rp.RoleId == roleId && rp.PermissionId == permissionId, ct);

            if (existing != null)
                return;

            var rolePermission = new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            };

            await _dbContext.RolePermissions.AddAsync(rolePermission, ct);
        }

        public async Task RemoveFromRoleAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct)
        {
            var rolePermission = await _dbContext.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, ct);

            if (rolePermission != null)
                _dbContext.RolePermissions.Remove(rolePermission);
        }
    }
}
