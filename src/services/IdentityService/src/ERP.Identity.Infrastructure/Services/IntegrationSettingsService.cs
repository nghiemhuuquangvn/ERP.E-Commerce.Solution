using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Security;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>
    /// Storage+CRUD only for now. ExternalApiKey is encrypted via ISecretProtector before
    /// storage and never returned in plaintext — same pattern as PaymentService's
    /// TenantPaymentSettings.VnPaySecretKeyEncrypted (Phase 1).
    /// </summary>
    public class IntegrationSettingsService : IIntegrationSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISecretProtector _secretProtector;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "Integration";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public IntegrationSettingsService(IUnitOfWork unitOfWork, ISecretProtector secretProtector, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _secretProtector = secretProtector;
            _cache = cache;
        }

        public Task<SystemIntegrationSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemIntegrationSettingsDto> UpdateSystemSettingsAsync(UpdateSystemIntegrationSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.ExternalIntegrationsAllowed);
            await _unitOfWork.SystemIntegrationSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantIntegrationSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantIntegrationSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantIntegrationSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            var encryptedApiKey = string.IsNullOrEmpty(request.ExternalApiKey) ? null : _secretProtector.Protect(request.ExternalApiKey);
            settings.Update(request.ExternalApiBaseUrl, encryptedApiKey, request.SyncEnabled);
            await _unitOfWork.TenantIntegrationSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        private async Task<SystemIntegrationSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemIntegrationSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemIntegrationSettings.CreateDefault();
            await _unitOfWork.SystemIntegrationSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantIntegrationSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantIntegrationSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantIntegrationSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantIntegrationSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemIntegrationSettingsDto ToDto(SystemIntegrationSettings s) =>
            new(s.ExternalIntegrationsAllowed);

        private static TenantIntegrationSettingsDto ToDto(TenantIntegrationSettings s) =>
            new(s.TenantId, s.ExternalApiBaseUrl, !string.IsNullOrEmpty(s.ExternalApiKeyEncrypted), s.SyncEnabled);

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

        private static string SystemCacheKey => "settings:identity:integration:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:integration:tenant:{tenantId}";
    }
}
