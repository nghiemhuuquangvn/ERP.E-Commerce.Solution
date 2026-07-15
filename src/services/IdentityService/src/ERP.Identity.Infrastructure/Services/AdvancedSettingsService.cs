using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>Storage+CRUD only for now — genuine catch-all category, no runtime wiring.</summary>
    public class AdvancedSettingsService : IAdvancedSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdvancedSettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SystemAdvancedSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<SystemAdvancedSettingsDto> UpdateSystemSettingsAsync(UpdateSystemAdvancedSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.DebugModeAllowed);
            await _unitOfWork.SystemAdvancedSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantAdvancedSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantAdvancedSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantAdvancedSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.DebugModeEnabled, request.CustomCssUrl, request.DataExportEnabled);
            await _unitOfWork.TenantAdvancedSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        private async Task<SystemAdvancedSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemAdvancedSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemAdvancedSettings.CreateDefault();
            await _unitOfWork.SystemAdvancedSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantAdvancedSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantAdvancedSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantAdvancedSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantAdvancedSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemAdvancedSettingsDto ToDto(SystemAdvancedSettings s) =>
            new(s.DebugModeAllowed);

        private static TenantAdvancedSettingsDto ToDto(TenantAdvancedSettings s) =>
            new(s.TenantId, s.DebugModeEnabled, s.CustomCssUrl, s.DataExportEnabled);
    }
}
