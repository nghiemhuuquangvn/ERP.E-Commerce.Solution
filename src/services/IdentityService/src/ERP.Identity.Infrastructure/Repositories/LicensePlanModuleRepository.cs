using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class LicensePlanModuleRepository : GenericRepository<LicensePlanModule>, ILicensePlanModuleRepository
    {
        public LicensePlanModuleRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<LicensePlanModule>> GetByPlanIdAsync(Guid licensePlanId, CancellationToken ct)
        {
            return await _dbContext.Set<LicensePlanModule>().AsNoTracking()
                .Where(m => m.LicensePlanId == licensePlanId)
                .ToListAsync(ct);
        }

        public async Task DeleteByPlanIdAsync(Guid licensePlanId, CancellationToken ct)
        {
            var existing = await _dbContext.Set<LicensePlanModule>()
                .Where(m => m.LicensePlanId == licensePlanId)
                .ToListAsync(ct);
            _dbContext.Set<LicensePlanModule>().RemoveRange(existing);
        }
    }
}
