using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>Storage+CRUD only for now — not yet wired into any approval-chain runtime behavior.</summary>
    public class WorkflowSettingsService : IWorkflowSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public WorkflowSettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SystemWorkflowSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<SystemWorkflowSettingsDto> UpdateSystemSettingsAsync(UpdateSystemWorkflowSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.MultiLevelApprovalAvailable);
            await _unitOfWork.SystemWorkflowSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantWorkflowSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantWorkflowSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantWorkflowSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.MultiLevelApprovalEnabled, request.DefaultApprovalLevels, request.AutoApproveThresholdAmount);
            await _unitOfWork.TenantWorkflowSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
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
    }
}
