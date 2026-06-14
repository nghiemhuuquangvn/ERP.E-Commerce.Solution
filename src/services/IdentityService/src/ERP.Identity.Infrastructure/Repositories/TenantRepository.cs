using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace ERP.Identity.Infrastructure.Repositories
{
    public class TenantRepository : GenericRepository<AuthTenant>, ITenantRepository
    {
        public TenantRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<AuthTenant> GetTenantByCodeAsync(string tenantCode, CancellationToken cancellationToken)
        {
            return await _dbContext.Tenants.FirstOrDefaultAsync(m => m.TenantCode == tenantCode, cancellationToken) ?? throw new InvalidOperationException("Not found the tenant");
        }

        public async Task<IReadOnlyList<AuthTenant>> ListAllAsync(CancellationToken ct)
        {
            return await _dbContext.Tenants.AsNoTracking().ToListAsync(ct);
        }

        public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct)
        {
            return await _dbContext.Tenants.AsNoTracking()
                .AnyAsync(t => t.TenantCode == code, ct);
        }
    }
}