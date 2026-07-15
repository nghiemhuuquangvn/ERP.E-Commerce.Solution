using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>Storage+CRUD only for now — no IFileStorageService exists yet to read Provider/CdnBaseUrl from.</summary>
    public class FileStorageSettingsService : IFileStorageSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public FileStorageSettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SystemFileStorageSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<SystemFileStorageSettingsDto> UpdateSystemSettingsAsync(UpdateSystemFileStorageSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.CustomStorageProviderAllowed, request.DefaultMaxUploadSizeMb);
            await _unitOfWork.SystemFileStorageSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantFileStorageSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantFileStorageSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantFileStorageSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.Provider, request.MaxUploadSizeMb, request.CdnBaseUrl);
            await _unitOfWork.TenantFileStorageSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
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
    }
}
