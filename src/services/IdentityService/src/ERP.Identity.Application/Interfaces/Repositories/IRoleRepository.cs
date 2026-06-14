using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IRoleRepository : IGenericRepository<Role>
    {
        Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, Guid tenantId, CancellationToken ct);
        Task<List<Role>> GetRolesByCodeAsync(IEnumerable<string> codes, Guid tenantId, CancellationToken ct);
        Task<Role?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<Role>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
        Task<bool> ExistsAsync(string code, Guid tenantId, CancellationToken ct);
    }
}
