using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ITenantFeatureFlagOverrideRepository : IGenericRepository<TenantFeatureFlagOverride>
    {
        Task<IReadOnlyList<TenantFeatureFlagOverride>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct);
        Task<TenantFeatureFlagOverride?> GetByTenantAndKeyAsync(Guid tenantId, string key, CancellationToken ct);
    }
}
