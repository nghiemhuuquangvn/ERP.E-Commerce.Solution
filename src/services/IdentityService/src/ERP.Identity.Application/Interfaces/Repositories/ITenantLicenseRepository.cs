namespace ERP.Identity.Application.Interfaces.Repositories
{
    using ERP.Entity.Application.Interfaces.Repositories;
    using ERP.Identity.Domain.Entities;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITenantLicenseRepository : IGenericRepository<TenantLicense>
    {
        Task<TenantLicense?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct);
    }
}
