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
    public class RoleRepository : GenericRepository<Role>, IRoleRepository
    {
        public RoleRepository(AuthDbContext dbContext) : base(dbContext) { }

        public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            var roles = await (
                from ur in _dbContext.UserRoles.AsNoTracking()
                join r in _dbContext.Roles.AsNoTracking()
                    on ur.RoleId equals r.Id
                where ur.UserId == userId
                    && ur.TenantId == tenantId
                    && r.TenantId == tenantId
                    && r.IsActive
                select r.RoleCode
            )
            .Distinct()
            .ToListAsync(ct);

            return roles;
        }

        public async Task<List<Role>> GetRolesByCodeAsync(IEnumerable<string> codes, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Roles
                                .Where(r => r.TenantId == tenantId && codes.Contains(r.RoleCode))
                                .ToListAsync(ct);
        }

        public async Task<Role?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleCode == code && r.TenantId == tenantId, ct);
        }

        public async Task<IReadOnlyList<Role>> ListByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Roles.AsNoTracking()
                .Where(r => r.TenantId == tenantId)
                .ToListAsync(ct);
        }

        public async Task<bool> ExistsAsync(string code, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Roles.AsNoTracking()
                .AnyAsync(r => r.RoleCode == code && r.TenantId == tenantId, ct);
        }

        public async Task<Role?> GetByIdInTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
        }
    }
}
