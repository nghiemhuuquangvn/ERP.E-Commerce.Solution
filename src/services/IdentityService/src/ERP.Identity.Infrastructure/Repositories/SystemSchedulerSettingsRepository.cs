using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemSchedulerSettingsRepository : GenericRepository<SystemSchedulerSettings>, ISystemSchedulerSettingsRepository
    {
        public SystemSchedulerSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemSchedulerSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemSchedulerSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
