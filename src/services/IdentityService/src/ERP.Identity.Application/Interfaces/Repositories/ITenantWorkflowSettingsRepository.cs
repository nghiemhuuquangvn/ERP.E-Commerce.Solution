using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ITenantWorkflowSettingsRepository : IGenericRepository<TenantWorkflowSettings>
    {
        Task<TenantWorkflowSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct);
    }
}
