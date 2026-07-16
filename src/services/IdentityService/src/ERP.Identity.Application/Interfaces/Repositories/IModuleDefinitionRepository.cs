namespace ERP.Identity.Application.Interfaces.Repositories
{
    using ERP.Entity.Application.Interfaces.Repositories;
    using ERP.Identity.Domain.Entities;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IModuleDefinitionRepository : IGenericRepository<ModuleDefinition>
    {
        Task<IReadOnlyList<ModuleDefinition>> ListAllAsync(CancellationToken ct);
        Task<ModuleDefinition?> GetByCodeAsync(string moduleCode, CancellationToken ct);
    }
}
