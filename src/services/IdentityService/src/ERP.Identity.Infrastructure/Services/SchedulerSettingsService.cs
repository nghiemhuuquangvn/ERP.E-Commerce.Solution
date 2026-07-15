using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>Storage+CRUD only for now — does not gate OutboxProcessor's actual polling interval yet.</summary>
    public class SchedulerSettingsService : ISchedulerSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SchedulerSettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SystemSchedulerSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<SystemSchedulerSettingsDto> UpdateSystemSettingsAsync(UpdateSystemSchedulerSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.BackgroundJobsEnabled);
            await _unitOfWork.SystemSchedulerSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantSchedulerSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantSchedulerSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantSchedulerSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.OutboxPollingIntervalSeconds, request.MaxRetryAttempts);
            await _unitOfWork.TenantSchedulerSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
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
    }
}
