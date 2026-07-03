using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IApiKeyRepository : IGenericRepository<ApiKey>
    {
        Task<ApiKey?> GetByPrefixAsync(string keyPrefix, CancellationToken ct);
        Task<IReadOnlyList<ApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
    }
}
