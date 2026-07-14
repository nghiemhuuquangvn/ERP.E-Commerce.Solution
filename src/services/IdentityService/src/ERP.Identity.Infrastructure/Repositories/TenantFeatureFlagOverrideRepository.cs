using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class TenantFeatureFlagOverrideRepository : GenericRepository<TenantFeatureFlagOverride>, ITenantFeatureFlagOverrideRepository
    {
        public TenantFeatureFlagOverrideRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<TenantFeatureFlagOverride>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<TenantFeatureFlagOverride>().AsNoTracking()
                .Where(f => f.TenantId == tenantId)
                .ToListAsync(ct);
        }

        public async Task<TenantFeatureFlagOverride?> GetByTenantAndKeyAsync(Guid tenantId, string key, CancellationToken ct)
        {
            return await _dbContext.Set<TenantFeatureFlagOverride>()
                .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Key == key, ct);
        }
    }
}
