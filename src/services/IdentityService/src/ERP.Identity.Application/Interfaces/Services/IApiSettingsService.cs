using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IApiSettingsService
    {
        Task<SystemApiSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemApiSettingsDto> UpdateSystemSettingsAsync(UpdateSystemApiSettingsRequest request, CancellationToken ct);
        Task<TenantApiSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantApiSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantApiSettingsRequest request, CancellationToken ct);
    }
}
