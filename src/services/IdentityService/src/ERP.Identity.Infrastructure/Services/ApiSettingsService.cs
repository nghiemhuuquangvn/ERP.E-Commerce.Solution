using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>
    /// Storage+CRUD only for now. Config for the existing ApiKey/ApiKeysController feature and
    /// Ocelot Gateway's rate limiting — does not re-model ApiKey and does not yet gate either.
    /// </summary>
    public class ApiSettingsService : IApiSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "Api";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public ApiSettingsService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<SystemApiSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemApiSettingsDto> UpdateSystemSettingsAsync(UpdateSystemApiSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.ApiKeysAllowed, request.DefaultRateLimitPerMinute);
            await _unitOfWork.SystemApiSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantApiSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantApiSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantApiSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.RateLimitPerMinute, request.MaxActiveApiKeys);
            await _unitOfWork.TenantApiSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        private async Task<SystemApiSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemApiSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemApiSettings.CreateDefault();
            await _unitOfWork.SystemApiSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantApiSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantApiSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantApiSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantApiSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemApiSettingsDto ToDto(SystemApiSettings s) =>
            new(s.ApiKeysAllowed, s.DefaultRateLimitPerMinute);

        private static TenantApiSettingsDto ToDto(TenantApiSettings s) =>
            new(s.TenantId, s.RateLimitPerMinute, s.MaxActiveApiKeys);

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

        private static string SystemCacheKey => "settings:identity:api:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:api:tenant:{tenantId}";
    }
}
