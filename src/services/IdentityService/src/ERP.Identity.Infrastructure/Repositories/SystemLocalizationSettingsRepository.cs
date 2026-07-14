using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemLocalizationSettingsRepository : GenericRepository<SystemLocalizationSettings>, ISystemLocalizationSettingsRepository
    {
        public SystemLocalizationSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemLocalizationSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemLocalizationSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
