using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
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

        public GeneralSettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SystemGeneralSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<SystemGeneralSettingsDto> UpdateSystemSettingsAsync(UpdateSystemGeneralSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.MaintenanceModeEnabled, request.DefaultLanguageCode);
            await _unitOfWork.SystemGeneralSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantGeneralSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantGeneralSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantGeneralSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.SiteName, request.DefaultLanguageCode, request.TimeZone, request.DateFormat, request.MaintenanceModeEnabled);
            await _unitOfWork.TenantGeneralSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
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
    }
}
