using ERP.Identity.Application.DTOs.License;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface ILicenseService
    {
        Task<List<ModuleDefinitionDto>> GetModulesAsync(CancellationToken ct);

        /// <summary>moduleCode comes from the route — see UpsertModuleRequest's doc comment.</summary>
        Task<ModuleDefinitionDto> UpsertModuleAsync(string moduleCode, UpsertModuleRequest request, CancellationToken ct);
        Task<List<LicensePlanDto>> GetPlansAsync(CancellationToken ct);

        /// <summary>planCode comes from the route — see UpsertLicensePlanRequest's doc comment.</summary>
        Task<LicensePlanDto> UpsertPlanAsync(string planCode, UpsertLicensePlanRequest request, CancellationToken ct);
        Task<TenantLicenseDto?> GetTenantLicenseAsync(Guid tenantId, CancellationToken ct);
        Task<TenantLicenseDto> IssueLicenseAsync(Guid tenantId, IssueLicenseRequest request, string issuedByUserId, CancellationToken ct);
        Task<TenantLicenseDto> RenewLicenseAsync(Guid tenantId, RenewLicenseRequest request, CancellationToken ct);
        Task SuspendLicenseAsync(Guid tenantId, CancellationToken ct);
        Task RevokeLicenseAsync(Guid tenantId, CancellationToken ct);

        /// <summary>
        /// Used by AuthService at login/refresh to embed "licensed_module" JWT claims.
        /// Returns [] if no license exists or the current one isn't valid (respects
        /// IsCurrentlyValid — Status + ExpiresAt + StartsAt), else the licensed module codes.
        /// </summary>
        Task<string[]> GetEffectiveModulesAsync(Guid tenantId, CancellationToken ct);
    }
}
