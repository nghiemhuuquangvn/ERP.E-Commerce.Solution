using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ITenantAuthSettingsRepository : IGenericRepository<TenantAuthSettings>
    {
        Task<TenantAuthSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct);
    }
}
