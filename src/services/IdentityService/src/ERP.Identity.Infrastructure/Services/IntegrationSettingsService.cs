using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Shared.Security;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>
    /// Storage+CRUD only for now. ExternalApiKey is encrypted via ISecretProtector before
    /// storage and never returned in plaintext — same pattern as PaymentService's
    /// TenantPaymentSettings.VnPaySecretKeyEncrypted (Phase 1).
    /// </summary>
    public class IntegrationSettingsService : IIntegrationSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISecretProtector _secretProtector;

        public IntegrationSettingsService(IUnitOfWork unitOfWork, ISecretProtector secretProtector)
        {
            _unitOfWork = unitOfWork;
            _secretProtector = secretProtector;
        }

        public async Task<SystemIntegrationSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<SystemIntegrationSettingsDto> UpdateSystemSettingsAsync(UpdateSystemIntegrationSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.ExternalIntegrationsAllowed);
            await _unitOfWork.SystemIntegrationSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantIntegrationSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantIntegrationSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantIntegrationSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            var encryptedApiKey = string.IsNullOrEmpty(request.ExternalApiKey) ? null : _secretProtector.Protect(request.ExternalApiKey);
            settings.Update(request.ExternalApiBaseUrl, encryptedApiKey, request.SyncEnabled);
            await _unitOfWork.TenantIntegrationSettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        private async Task<SystemIntegrationSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemIntegrationSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemIntegrationSettings.CreateDefault();
            await _unitOfWork.SystemIntegrationSettings.AddAsync(settings, ct);
            return settings;
        }

        private async Task<TenantIntegrationSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantIntegrationSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantIntegrationSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantIntegrationSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemIntegrationSettingsDto ToDto(SystemIntegrationSettings s) =>
            new(s.ExternalIntegrationsAllowed);

        private static TenantIntegrationSettingsDto ToDto(TenantIntegrationSettings s) =>
            new(s.TenantId, s.ExternalApiBaseUrl, !string.IsNullOrEmpty(s.ExternalApiKeyEncrypted), s.SyncEnabled);
    }
}
