using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class TenantLocalizationSettingsRepository : GenericRepository<TenantLocalizationSettings>, ITenantLocalizationSettingsRepository
    {
        public TenantLocalizationSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<TenantLocalizationSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<TenantLocalizationSettings>().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        }
    }
}
