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
    /// <summary>Storage+CRUD only for now — does not gate OutboxProcessor's actual polling interval yet.</summary>
    public class SchedulerSettingsService : ISchedulerSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "Scheduler";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public SchedulerSettingsService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<SystemSchedulerSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemSchedulerSettingsDto> UpdateSystemSettingsAsync(UpdateSystemSchedulerSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.BackgroundJobsEnabled);
            await _unitOfWork.SystemSchedulerSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantSchedulerSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantSchedulerSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantSchedulerSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.OutboxPollingIntervalSeconds, request.MaxRetryAttempts);
            await _unitOfWork.TenantSchedulerSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        private async Task<SystemSchedulerSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemSchedulerSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemSchedulerSettings.CreateDefault();
            await _unitOfWork.SystemSchedulerSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantSchedulerSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantSchedulerSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantSchedulerSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantSchedulerSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemSchedulerSettingsDto ToDto(SystemSchedulerSettings s) =>
            new(s.BackgroundJobsEnabled);

        private static TenantSchedulerSettingsDto ToDto(TenantSchedulerSettings s) =>
            new(s.TenantId, s.OutboxPollingIntervalSeconds, s.MaxRetryAttempts);

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

        private static string SystemCacheKey => "settings:identity:scheduler:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:scheduler:tenant:{tenantId}";
    }
}
