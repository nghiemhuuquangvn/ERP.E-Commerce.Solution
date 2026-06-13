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
        public Task AssignPermissionToRoleAsync(Guid roleId, string permissionCode, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public Task<bool> ExistsAsync(string permissionCode, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<string>> GetAllAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task RemovePermissionFromRoleAsync(Guid roleId, string permissionCode, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, IEnumerable<string> permissionCodes)
        {
            return await _dbContext.UserRoles.AsNoTracking()
                            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId)
                            .SelectMany(ur => _dbContext.RolePermissions.AsNoTracking()
                                .Where(rp => rp.RoleId == ur.RoleId && rp.TenantId == tenantId))
                            .AnyAsync(p => permissionCodes.Contains(p.PermissionId.ToString()));
        }
    }
}
