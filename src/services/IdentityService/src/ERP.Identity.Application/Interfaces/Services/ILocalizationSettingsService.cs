using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface ILocalizationSettingsService
    {
        Task<SystemLocalizationSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemLocalizationSettingsDto> UpdateSystemSettingsAsync(UpdateSystemLocalizationSettingsRequest request, CancellationToken ct);
        Task<TenantLocalizationSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantLocalizationSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantLocalizationSettingsRequest request, CancellationToken ct);
    }
}
