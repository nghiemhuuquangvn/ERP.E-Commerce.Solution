using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemAuthSettingsRepository : GenericRepository<SystemAuthSettings>, ISystemAuthSettingsRepository
    {
        public SystemAuthSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemAuthSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemAuthSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
