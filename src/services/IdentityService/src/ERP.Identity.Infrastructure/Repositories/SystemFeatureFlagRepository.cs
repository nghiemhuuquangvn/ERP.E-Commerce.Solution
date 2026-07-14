using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SystemFeatureFlagRepository : GenericRepository<SystemFeatureFlag>, ISystemFeatureFlagRepository
    {
        public SystemFeatureFlagRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<SystemFeatureFlag>> ListAllAsync(CancellationToken ct)
        {
            return await _dbContext.Set<SystemFeatureFlag>().AsNoTracking()
                .OrderBy(f => f.Key)
                .ToListAsync(ct);
        }

        public async Task<SystemFeatureFlag?> GetByKeyAsync(string key, CancellationToken ct)
        {
            return await _dbContext.Set<SystemFeatureFlag>().FirstOrDefaultAsync(f => f.Key == key, ct);
        }
    }
}
