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
    /// <summary>Storage+CRUD only for now — no IFileStorageService exists yet to read Provider/CdnBaseUrl from.</summary>
    public class FileStorageSettingsService : IFileStorageSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "FileStorage";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public FileStorageSettingsService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<SystemFileStorageSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemFileStorageSettingsDto> UpdateSystemSettingsAsync(UpdateSystemFileStorageSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.CustomStorageProviderAllowed, request.DefaultMaxUploadSizeMb);
            await _unitOfWork.SystemFileStorageSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantFileStorageSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantFileStorageSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantFileStorageSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.Provider, request.MaxUploadSizeMb, request.CdnBaseUrl);
            await _unitOfWork.TenantFileStorageSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        private async Task<SystemFileStorageSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemFileStorageSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemFileStorageSettings.CreateDefault();
            await _unitOfWork.SystemFileStorageSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantFileStorageSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantFileStorageSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantFileStorageSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantFileStorageSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemFileStorageSettingsDto ToDto(SystemFileStorageSettings s) =>
            new(s.CustomStorageProviderAllowed, s.DefaultMaxUploadSizeMb);

        private static TenantFileStorageSettingsDto ToDto(TenantFileStorageSettings s) =>
            new(s.TenantId, s.Provider, s.MaxUploadSizeMb, s.CdnBaseUrl);

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

        private static string SystemCacheKey => "settings:identity:file-storage:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:file-storage:tenant:{tenantId}";
    }
}
