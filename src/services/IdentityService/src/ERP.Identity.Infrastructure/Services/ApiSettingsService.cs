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
    /// Storage+CRUD only for now. Config for the existing ApiKey/ApiKeysController feature and
    /// Ocelot Gateway's rate limiting — does not re-model ApiKey and does not yet gate either.
    /// </summary>
    public class ApiSettingsService : IApiSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ApiSettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SystemApiSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<SystemApiSettingsDto> UpdateSystemSettingsAsync(UpdateSystemApiSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.ApiKeysAllowed, request.DefaultRateLimitPerMinute);
            await _unitOfWork.SystemApiSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantApiSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantApiSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantApiSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.RateLimitPerMinute, request.MaxActiveApiKeys);
            await _unitOfWork.TenantApiSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        private async Task<SystemApiSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemApiSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemApiSettings.CreateDefault();
            await _unitOfWork.SystemApiSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantApiSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantApiSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantApiSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantApiSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemApiSettingsDto ToDto(SystemApiSettings s) =>
            new(s.ApiKeysAllowed, s.DefaultRateLimitPerMinute);

        private static TenantApiSettingsDto ToDto(TenantApiSettings s) =>
            new(s.TenantId, s.RateLimitPerMinute, s.MaxActiveApiKeys);
    }
}
