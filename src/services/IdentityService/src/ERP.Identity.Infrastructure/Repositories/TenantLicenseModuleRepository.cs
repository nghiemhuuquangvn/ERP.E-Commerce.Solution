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
    public class TenantLicenseModuleRepository : GenericRepository<TenantLicenseModule>, ITenantLicenseModuleRepository
    {
        public TenantLicenseModuleRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<TenantLicenseModule>> GetByTenantLicenseIdAsync(Guid tenantLicenseId, CancellationToken ct)
        {
            return await _dbContext.Set<TenantLicenseModule>().AsNoTracking()
                .Where(m => m.TenantLicenseId == tenantLicenseId)
                .ToListAsync(ct);
        }

        public async Task DeleteByTenantLicenseIdAsync(Guid tenantLicenseId, CancellationToken ct)
        {
            var existing = await _dbContext.Set<TenantLicenseModule>()
                .Where(m => m.TenantLicenseId == tenantLicenseId)
                .ToListAsync(ct);
            _dbContext.Set<TenantLicenseModule>().RemoveRange(existing);
        }
    }
}
