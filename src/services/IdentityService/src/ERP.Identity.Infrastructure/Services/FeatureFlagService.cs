using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>
    /// Storage+CRUD only for now. Deliberately dynamic key-value shape (not fixed boolean
    /// fields like every other Settings category) — admins define arbitrary flag keys.
    /// No feature-flag check anywhere in the codebase reads from these tables yet.
    /// </summary>
    public class FeatureFlagService : IFeatureFlagService
    {
        private readonly IUnitOfWork _unitOfWork;

        public FeatureFlagService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyList<SystemFeatureFlagDto>> GetSystemFlagsAsync(CancellationToken ct)
        {
            var flags = await _unitOfWork.SystemFeatureFlags.ListAllAsync(ct);
            return flags.Select(ToDto).ToList();
        }

        public async Task<SystemFeatureFlagDto> UpsertSystemFlagAsync(string key, UpsertSystemFeatureFlagRequest request, CancellationToken ct)
        {
            var flag = await _unitOfWork.SystemFeatureFlags.GetByKeyAsync(key, ct);
            if (flag == null)
            {
                flag = SystemFeatureFlag.Create(key, request.IsEnabled, request.Description);
                await _unitOfWork.SystemFeatureFlags.AddAsync(flag, ct);
            }
            else
            {
                flag.Update(request.IsEnabled, request.Description);
                await _unitOfWork.SystemFeatureFlags.UpdateAsync(flag, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(flag);
        }

        public async Task<IReadOnlyList<TenantFeatureFlagDto>> GetTenantFlagsAsync(Guid tenantId, CancellationToken ct)
        {
            var systemFlags = await _unitOfWork.SystemFeatureFlags.ListAllAsync(ct);
            var overrides = await _unitOfWork.TenantFeatureFlagOverrides.GetByTenantIdAsync(tenantId, ct);
            var overridesByKey = overrides.ToDictionary(o => o.Key, o => o);

            return systemFlags
                .Select(f => overridesByKey.TryGetValue(f.Key, out var o)
                    ? new TenantFeatureFlagDto(f.Key, o.IsEnabled, true, f.Description)
                    : new TenantFeatureFlagDto(f.Key, f.IsEnabled, false, f.Description))
                .ToList();
        }

        public async Task<TenantFeatureFlagDto> SetTenantFlagOverrideAsync(Guid tenantId, string key, SetTenantFeatureFlagOverrideRequest request, CancellationToken ct)
        {
            var systemFlag = await _unitOfWork.SystemFeatureFlags.GetByKeyAsync(key, ct)
                ?? throw new ArgumentException($"Feature flag '{key}' does not exist", nameof(key));

            var flagOverride = await _unitOfWork.TenantFeatureFlagOverrides.GetByTenantAndKeyAsync(tenantId, key, ct);
            if (flagOverride == null)
            {
                flagOverride = TenantFeatureFlagOverride.Create(tenantId, key, request.IsEnabled);
                await _unitOfWork.TenantFeatureFlagOverrides.AddAsync(flagOverride, ct);
            }
            else
            {
                flagOverride.SetEnabled(request.IsEnabled);
                await _unitOfWork.TenantFeatureFlagOverrides.UpdateAsync(flagOverride, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return new TenantFeatureFlagDto(key, flagOverride.IsEnabled, true, systemFlag.Description);
        }

        private static SystemFeatureFlagDto ToDto(SystemFeatureFlag f) =>
            new(f.Key, f.IsEnabled, f.Description);
    }
}
