namespace ERP.Identity.Application.Interfaces.Repositories
{
    using ERP.Entity.Application.Interfaces.Repositories;
    using ERP.Identity.Domain.Entities;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILicensePlanModuleRepository : IGenericRepository<LicensePlanModule>
    {
        Task<IReadOnlyList<LicensePlanModule>> GetByPlanIdAsync(Guid licensePlanId, CancellationToken ct);
        Task DeleteByPlanIdAsync(Guid licensePlanId, CancellationToken ct);
    }
}
