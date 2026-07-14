using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ITenantCompanySettingsRepository : IGenericRepository<TenantCompanySettings>
    {
        Task<TenantCompanySettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct);
    }
}
