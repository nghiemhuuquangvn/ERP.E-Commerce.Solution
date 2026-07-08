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

        /// <summary>Tenant-boundary-safe fetch — null if the role doesn't exist OR
        /// belongs to a different tenant (a Role has no cross-tenant concept, unlike
        /// User, so this is a simple equality check).</summary>
        Task<Role?> GetByIdInTenantAsync(Guid id, Guid tenantId, CancellationToken ct);
    }
}
