using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemGeneralSettingsRepository : GenericRepository<SystemGeneralSettings>, ISystemGeneralSettingsRepository
    {
        public SystemGeneralSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemGeneralSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemGeneralSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
