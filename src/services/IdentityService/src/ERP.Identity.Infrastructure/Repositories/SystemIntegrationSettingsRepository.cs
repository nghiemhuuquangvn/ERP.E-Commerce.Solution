using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemIntegrationSettingsRepository : GenericRepository<SystemIntegrationSettings>, ISystemIntegrationSettingsRepository
    {
        public SystemIntegrationSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemIntegrationSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemIntegrationSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
