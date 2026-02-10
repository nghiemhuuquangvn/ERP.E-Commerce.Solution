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
        private readonly AuthDbContext _dbContext;
        public RoleRepository(AuthDbContext dbContext) : base(dbContext) { _dbContext = dbContext; }
        
        public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            var roles = await (
                from ur in _dbContext.UserRoles.AsNoTracking()
                join r in _dbContext.Roles.AsNoTracking()
                    on ur.RoleId equals r.Id
                where ur.UserId == userId
                    && ur.TenantId == tenantId
                    && ur.IsActive
                    && r.IsActive
                select r.RoleCode
            )
            .Distinct()
            .ToListAsync(ct);

            return roles;
        }
    }
}
