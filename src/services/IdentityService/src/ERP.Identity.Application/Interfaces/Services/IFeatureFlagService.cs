using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IFeatureFlagService
    {
        Task<IReadOnlyList<SystemFeatureFlagDto>> GetSystemFlagsAsync(CancellationToken ct);
        Task<SystemFeatureFlagDto> UpsertSystemFlagAsync(string key, UpsertSystemFeatureFlagRequest request, CancellationToken ct);
        Task<IReadOnlyList<TenantFeatureFlagDto>> GetTenantFlagsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantFeatureFlagDto> SetTenantFlagOverrideAsync(Guid tenantId, string key, SetTenantFeatureFlagOverrideRequest request, CancellationToken ct);
    }
}
