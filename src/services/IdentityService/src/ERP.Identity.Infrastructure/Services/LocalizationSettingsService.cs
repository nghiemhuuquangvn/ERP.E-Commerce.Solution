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
    /// Storage+CRUD only for now. Deeper i18n config than General Settings' DefaultLanguageCode
    /// — the supported language list itself remains owned by SharedMasterDataService.
    /// </summary>
    public class LocalizationSettingsService : ILocalizationSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "Localization";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public LocalizationSettingsService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<SystemLocalizationSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemLocalizationSettingsDto> UpdateSystemSettingsAsync(UpdateSystemLocalizationSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.MultiLanguageAllowed, request.DefaultLocale);
            await _unitOfWork.SystemLocalizationSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantLocalizationSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantLocalizationSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantLocalizationSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.DefaultLocale, request.SupportedLocales, request.NumberFormatLocale, request.RtlEnabled);
            await _unitOfWork.TenantLocalizationSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        private async Task<SystemLocalizationSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemLocalizationSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemLocalizationSettings.CreateDefault();
            await _unitOfWork.SystemLocalizationSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantLocalizationSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantLocalizationSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantLocalizationSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantLocalizationSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemLocalizationSettingsDto ToDto(SystemLocalizationSettings s) =>
            new(s.MultiLanguageAllowed, s.DefaultLocale);

        private static TenantLocalizationSettingsDto ToDto(TenantLocalizationSettings s) =>
            new(s.TenantId, s.DefaultLocale, s.SupportedLocales, s.NumberFormatLocale, s.RtlEnabled);

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

        private static string SystemCacheKey => "settings:identity:localization:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:localization:tenant:{tenantId}";
    }
}
