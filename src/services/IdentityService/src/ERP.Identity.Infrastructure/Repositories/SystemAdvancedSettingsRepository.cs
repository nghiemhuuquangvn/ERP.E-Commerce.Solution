using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemAdvancedSettingsRepository : GenericRepository<SystemAdvancedSettings>, ISystemAdvancedSettingsRepository
    {
        public SystemAdvancedSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemAdvancedSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemAdvancedSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
