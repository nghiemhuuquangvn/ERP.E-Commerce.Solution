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
    public class LicensePlanRepository : GenericRepository<LicensePlan>, ILicensePlanRepository
    {
        public LicensePlanRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<LicensePlan>> ListAllAsync(CancellationToken ct)
        {
            return await _dbContext.Set<LicensePlan>().AsNoTracking()
                .OrderBy(p => p.PlanCode)
                .ToListAsync(ct);
        }

        public async Task<LicensePlan?> GetByCodeAsync(string planCode, CancellationToken ct)
        {
            return await _dbContext.Set<LicensePlan>().FirstOrDefaultAsync(p => p.PlanCode == planCode, ct);
        }
    }
}
