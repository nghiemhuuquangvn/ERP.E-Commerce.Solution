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
    /// Storage+CRUD only for now. Tenant-only — a company profile has no platform-wide
    /// System tier (there is no "default company" at the platform level).
    /// </summary>
    public class CompanySettingsService : ICompanySettingsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CompanySettingsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<TenantCompanySettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        public async Task<TenantCompanySettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantCompanySettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.CompanyName, request.LegalName, request.TaxCode, request.Address, request.Phone, request.Email, request.LogoUrl, request.Website);
            await _unitOfWork.TenantCompanySettings.UpdateAsync(settings, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(settings);
        }

        private async Task<TenantCompanySettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantCompanySettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantCompanySettings.CreateDefault(tenantId);
            await _unitOfWork.TenantCompanySettings.AddAsync(settings, ct);
            return settings;
        }

        private static TenantCompanySettingsDto ToDto(TenantCompanySettings s) =>
            new(s.TenantId, s.CompanyName, s.LegalName, s.TaxCode, s.Address, s.Phone, s.Email, s.LogoUrl, s.Website);
    }
}
