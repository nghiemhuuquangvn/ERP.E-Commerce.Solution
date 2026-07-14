using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ISystemFeatureFlagRepository : IGenericRepository<SystemFeatureFlag>
    {
        Task<IReadOnlyList<SystemFeatureFlag>> ListAllAsync(CancellationToken ct);
        Task<SystemFeatureFlag?> GetByKeyAsync(string key, CancellationToken ct);
    }
}
