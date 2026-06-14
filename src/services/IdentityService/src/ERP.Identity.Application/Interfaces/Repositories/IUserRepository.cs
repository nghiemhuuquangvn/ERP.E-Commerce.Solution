using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct);
        Task<User> GetByExternalProviderAsync(string provider, string providerKey, Guid tenantId, CancellationToken ct);
        Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct);
        Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct);
        Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct);
    }
}
