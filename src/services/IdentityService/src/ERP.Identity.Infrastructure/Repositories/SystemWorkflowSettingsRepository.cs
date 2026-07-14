using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemWorkflowSettingsRepository : GenericRepository<SystemWorkflowSettings>, ISystemWorkflowSettingsRepository
    {
        public SystemWorkflowSettingsRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SystemWorkflowSettings?> GetAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemWorkflowSettings>().FirstOrDefaultAsync(ct);
        }
    }
}
