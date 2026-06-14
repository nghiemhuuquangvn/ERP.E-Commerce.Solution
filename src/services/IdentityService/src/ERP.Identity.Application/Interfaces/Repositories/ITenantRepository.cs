using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ITenantRepository : IGenericRepository<AuthTenant>
    {
        Task<AuthTenant> GetTenantByCodeAsync(string tenantCode, CancellationToken cancellationToken);
        Task<IReadOnlyList<AuthTenant>> ListAllAsync(CancellationToken ct);
        Task<bool> ExistsByCodeAsync(string code, CancellationToken ct);
    }
}