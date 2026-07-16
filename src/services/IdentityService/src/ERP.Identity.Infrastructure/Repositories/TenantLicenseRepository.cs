using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class TenantLicenseRepository : GenericRepository<TenantLicense>, ITenantLicenseRepository
    {
        public TenantLicenseRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<TenantLicense?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<TenantLicense>().FirstOrDefaultAsync(l => l.TenantId == tenantId, ct);
        }
    }
}
