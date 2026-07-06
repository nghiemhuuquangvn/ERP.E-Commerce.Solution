using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SystemAuthSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<SystemAuthSettingsDto> UpdateSystemSettingsAsync(UpdateSystemAuthSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.GoogleEnabled, request.MicrosoftEnabled, request.FacebookEnabled, request.TwitterEnabled, request.TwoFactorEnabled);
            await _unitOfWork.SystemAuthSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantAuthSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantAuthSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantAuthSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.GoogleEnabled, request.MicrosoftEnabled, request.FacebookEnabled, request.TwitterEnabled, request.TwoFactorEnabled, request.TwoFactorRequired);
            await _unitOfWork.TenantAuthSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<PublicAuthOptionsDto> GetPublicOptionsAsync(string tenantCode, CancellationToken ct)
        {
            if (!await _unitOfWork.Tenants.ExistsByCodeAsync(tenantCode, ct))
                return new PublicAuthOptionsDto(false, false, false, false, false);

            var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(tenantCode, ct);

            var systemSettings = await GetOrCreateSystemSettingsEntityAsync(ct);
            var tenantSettings = await GetOrCreateTenantSettingsEntityAsync(tenant.Id, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new PublicAuthOptionsDto(
                GoogleEnabled: systemSettings.GoogleEnabled && tenantSettings.GoogleEnabled,
                MicrosoftEnabled: systemSettings.MicrosoftEnabled && tenantSettings.MicrosoftEnabled,
                FacebookEnabled: systemSettings.FacebookEnabled && tenantSettings.FacebookEnabled,
                TwitterEnabled: systemSettings.TwitterEnabled && tenantSettings.TwitterEnabled,
                TwoFactorEnabled: systemSettings.TwoFactorEnabled && tenantSettings.TwoFactorEnabled);
        }

        public async Task<bool> IsProviderEnabledAsync(Guid tenantId, string provider, CancellationToken ct)
        {
            var systemSettings = await GetOrCreateSystemSettingsEntityAsync(ct);
            var tenantSettings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return provider.ToUpperInvariant() switch
            {
                "GOOGLE" => systemSettings.GoogleEnabled && tenantSettings.GoogleEnabled,
                "MICROSOFT" => systemSettings.MicrosoftEnabled && tenantSettings.MicrosoftEnabled,
                "FACEBOOK" => systemSettings.FacebookEnabled && tenantSettings.FacebookEnabled,
                "TWITTER" => systemSettings.TwitterEnabled && tenantSettings.TwitterEnabled,
                _ => false
            };
        }

        public bool IsTwoFactorRequiredForUser(Domain.Entities.User user, SystemAuthSettings systemSettings, TenantAuthSettings tenantSettings)
        {
            return systemSettings.TwoFactorEnabled
                && tenantSettings.TwoFactorEnabled
                && (tenantSettings.TwoFactorRequired || user.TwoFactorEnabled);
        }

        public async Task<SystemAuthSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemAuthSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemAuthSettings.CreateDefault();
            await _unitOfWork.SystemAuthSettings.AddAsync(settings, ct);
            return settings;
        }

        public async Task<TenantAuthSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantAuthSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantAuthSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantAuthSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemAuthSettingsDto ToDto(SystemAuthSettings s) =>
            new(s.GoogleEnabled, s.MicrosoftEnabled, s.FacebookEnabled, s.TwitterEnabled, s.TwoFactorEnabled);

        private static TenantAuthSettingsDto ToDto(TenantAuthSettings s) =>
            new(s.TenantId, s.GoogleEnabled, s.MicrosoftEnabled, s.FacebookEnabled, s.TwitterEnabled, s.TwoFactorEnabled, s.TwoFactorRequired);
    }
}
