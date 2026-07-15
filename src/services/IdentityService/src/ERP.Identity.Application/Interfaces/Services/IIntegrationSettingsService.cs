using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IIntegrationSettingsService
    {
        Task<SystemIntegrationSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemIntegrationSettingsDto> UpdateSystemSettingsAsync(UpdateSystemIntegrationSettingsRequest request, CancellationToken ct);
        Task<TenantIntegrationSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantIntegrationSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantIntegrationSettingsRequest request, CancellationToken ct);
    }
}
