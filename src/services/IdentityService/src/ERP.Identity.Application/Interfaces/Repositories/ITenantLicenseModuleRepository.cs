namespace ERP.Identity.Application.Interfaces.Repositories
{
    using ERP.Entity.Application.Interfaces.Repositories;
    using ERP.Identity.Domain.Entities;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITenantLicenseModuleRepository : IGenericRepository<TenantLicenseModule>
    {
        Task<IReadOnlyList<TenantLicenseModule>> GetByTenantLicenseIdAsync(Guid tenantLicenseId, CancellationToken ct);
        Task DeleteByTenantLicenseIdAsync(Guid tenantLicenseId, CancellationToken ct);
    }
}
