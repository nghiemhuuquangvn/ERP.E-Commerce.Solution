using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class TenantIntegrationSettingsRepository : GenericRepository<TenantIntegrationSettings>, ITenantIntegrationSettingsRepository
    {
        public TenantIntegrationSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<TenantIntegrationSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<TenantIntegrationSettings>().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        }
    }
}
