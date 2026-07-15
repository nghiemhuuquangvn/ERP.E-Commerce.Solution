using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IAdvancedSettingsService
    {
        Task<SystemAdvancedSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemAdvancedSettingsDto> UpdateSystemSettingsAsync(UpdateSystemAdvancedSettingsRequest request, CancellationToken ct);
        Task<TenantAdvancedSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantAdvancedSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantAdvancedSettingsRequest request, CancellationToken ct);
    }
}
