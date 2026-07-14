using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemFileStorageSettingsRepository : GenericRepository<SystemFileStorageSettings>, ISystemFileStorageSettingsRepository
    {
        public SystemFileStorageSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemFileStorageSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemFileStorageSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
