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
    /// Storage+CRUD only for now. Config for the existing SecurityAuditLog/SecurityAuditService
    /// — does not yet gate what that service actually logs or how long it retains rows.
    /// </summary>
    public class AuditSettingsService : IAuditSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "Audit";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public AuditSettingsService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<SystemAuditSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemAuditSettingsDto> UpdateSystemSettingsAsync(UpdateSystemAuditSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.AuditLoggingEnabled);
            await _unitOfWork.SystemAuditSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantAuditSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantAuditSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantAuditSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.RetentionDays, request.LogSuccessfulLogins, request.LogFailedLogins, request.LogPermissionChanges);
            await _unitOfWork.TenantAuditSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        private async Task<SystemAuditSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemAuditSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemAuditSettings.CreateDefault();
            await _unitOfWork.SystemAuditSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantAuditSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantAuditSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantAuditSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantAuditSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemAuditSettingsDto ToDto(SystemAuditSettings s) =>
            new(s.AuditLoggingEnabled);

        private static TenantAuditSettingsDto ToDto(TenantAuditSettings s) =>
            new(s.TenantId, s.RetentionDays, s.LogSuccessfulLogins, s.LogFailedLogins, s.LogPermissionChanges);

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

        private static string SystemCacheKey => "settings:identity:audit:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:audit:tenant:{tenantId}";
    }
}
