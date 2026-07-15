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
    /// <summary>Storage+CRUD only for now — not yet wired into any approval-chain runtime behavior.</summary>
    public class WorkflowSettingsService : IWorkflowSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "Workflow";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public WorkflowSettingsService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<SystemWorkflowSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemWorkflowSettingsDto> UpdateSystemSettingsAsync(UpdateSystemWorkflowSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.MultiLevelApprovalAvailable);
            await _unitOfWork.SystemWorkflowSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantWorkflowSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantWorkflowSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantWorkflowSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.MultiLevelApprovalEnabled, request.DefaultApprovalLevels, request.AutoApproveThresholdAmount);
            await _unitOfWork.TenantWorkflowSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        private async Task<SystemWorkflowSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemWorkflowSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemWorkflowSettings.CreateDefault();
            await _unitOfWork.SystemWorkflowSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantWorkflowSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantWorkflowSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantWorkflowSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantWorkflowSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemWorkflowSettingsDto ToDto(SystemWorkflowSettings s) =>
            new(s.MultiLevelApprovalAvailable);

        private static TenantWorkflowSettingsDto ToDto(TenantWorkflowSettings s) =>
            new(s.TenantId, s.MultiLevelApprovalEnabled, s.DefaultApprovalLevels, s.AutoApproveThresholdAmount);

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

        private static string SystemCacheKey => "settings:identity:workflow:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:workflow:tenant:{tenantId}";
    }
}
