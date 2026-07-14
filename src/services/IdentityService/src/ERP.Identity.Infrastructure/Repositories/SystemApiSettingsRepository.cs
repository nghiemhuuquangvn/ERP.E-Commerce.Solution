using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemApiSettingsRepository : GenericRepository<SystemApiSettings>, ISystemApiSettingsRepository
    {
        public SystemApiSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemApiSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemApiSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
