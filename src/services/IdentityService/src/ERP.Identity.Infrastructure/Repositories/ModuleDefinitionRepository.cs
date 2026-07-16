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
    public class ModuleDefinitionRepository : GenericRepository<ModuleDefinition>, IModuleDefinitionRepository
    {
        public ModuleDefinitionRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<ModuleDefinition>> ListAllAsync(CancellationToken ct)
        {
            return await _dbContext.Set<ModuleDefinition>().AsNoTracking()
                .OrderBy(m => m.ModuleCode)
                .ToListAsync(ct);
        }

        public async Task<ModuleDefinition?> GetByCodeAsync(string moduleCode, CancellationToken ct)
        {
            return await _dbContext.Set<ModuleDefinition>().FirstOrDefaultAsync(m => m.ModuleCode == moduleCode, ct);
        }
    }
}
