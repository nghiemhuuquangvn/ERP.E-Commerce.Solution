using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemAuditSettingsRepository : GenericRepository<SystemAuditSettings>, ISystemAuditSettingsRepository
    {
        public SystemAuditSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemAuditSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemAuditSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
