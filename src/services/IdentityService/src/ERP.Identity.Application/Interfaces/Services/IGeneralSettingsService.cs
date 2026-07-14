using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IGeneralSettingsService
    {
        Task<SystemGeneralSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemGeneralSettingsDto> UpdateSystemSettingsAsync(UpdateSystemGeneralSettingsRequest request, CancellationToken ct);
        Task<TenantGeneralSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantGeneralSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantGeneralSettingsRequest request, CancellationToken ct);
    }
}
