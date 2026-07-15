using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using Newtonsoft.Json;
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
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "FeatureFlags";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public FeatureFlagService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<IReadOnlyList<SystemFeatureFlagDto>> GetSystemFlagsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var flags = await _unitOfWork.SystemFeatureFlags.ListAllAsync(ct);
                return (IReadOnlyList<SystemFeatureFlagDto>)flags.Select(ToDto).ToList();
            }, CacheTtl, ct);
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

            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(flag);
        }

        public Task<IReadOnlyList<TenantFeatureFlagDto>> GetTenantFlagsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var systemFlags = await _unitOfWork.SystemFeatureFlags.ListAllAsync(ct);
                var overrides = await _unitOfWork.TenantFeatureFlagOverrides.GetByTenantIdAsync(tenantId, ct);
                var overridesByKey = overrides.ToDictionary(o => o.Key, o => o);

                return (IReadOnlyList<TenantFeatureFlagDto>)systemFlags
                    .Select(f => overridesByKey.TryGetValue(f.Key, out var o)
                        ? new TenantFeatureFlagDto(f.Key, o.IsEnabled, true, f.Description)
                        : new TenantFeatureFlagDto(f.Key, f.IsEnabled, false, f.Description))
                    .ToList();
            }, CacheTtl, ct);
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

            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return new TenantFeatureFlagDto(key, flagOverride.IsEnabled, true, systemFlag.Description);
        }

        private static SystemFeatureFlagDto ToDto(SystemFeatureFlag f) =>
            new(f.Key, f.IsEnabled, f.Description);

        private async Task AddSettingsChangedOutboxMessageAsync(string scope, Guid? tenantId, CancellationToken ct)
        {
            var @event = new SettingsChangedIntegrationEvent { ServiceName = ServiceName, Category = Category, Scope = scope, TenantId = tenantId };
            var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOnUtc = DateTime.UtcNow,
                Type = nameof(SettingsChangedIntegrationEvent),
                Content = content
            };
            await _unitOfWork.OutboxMessages.AddAsync(outboxMessage, ct);
        }

        private static string SystemCacheKey => "settings:identity:feature-flags:system:all";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:feature-flags:tenant:{tenantId}:all";
    }
}
