using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class TenantAuditSettingsRepository : GenericRepository<TenantAuditSettings>, ITenantAuditSettingsRepository
    {
        public TenantAuditSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<TenantAuditSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<TenantAuditSettings>().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        }
    }
}
