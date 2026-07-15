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
    /// Storage+CRUD only for now — General Settings aren't yet wired into runtime
    /// behavior (maintenance-mode gate, default-language fallback). Mirrors
    /// SettingsService's lazy-create-on-read pattern for Auth Settings.
    /// </summary>
    public class GeneralSettingsService : IGeneralSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "General";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public GeneralSettingsService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<SystemGeneralSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemGeneralSettingsDto> UpdateSystemSettingsAsync(UpdateSystemGeneralSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.MaintenanceModeEnabled, request.DefaultLanguageCode);
            await _unitOfWork.SystemGeneralSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantGeneralSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantGeneralSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantGeneralSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.SiteName, request.DefaultLanguageCode, request.TimeZone, request.DateFormat, request.MaintenanceModeEnabled);
            await _unitOfWork.TenantGeneralSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        private async Task<SystemGeneralSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemGeneralSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemGeneralSettings.CreateDefault();
            await _unitOfWork.SystemGeneralSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantGeneralSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantGeneralSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantGeneralSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantGeneralSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemGeneralSettingsDto ToDto(SystemGeneralSettings s) =>
            new(s.MaintenanceModeEnabled, s.DefaultLanguageCode);

        private static TenantGeneralSettingsDto ToDto(TenantGeneralSettings s) =>
            new(s.TenantId, s.SiteName, s.DefaultLanguageCode, s.TimeZone, s.DateFormat, s.MaintenanceModeEnabled);

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

        private static string SystemCacheKey => "settings:identity:general:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:general:tenant:{tenantId}";
    }
}
