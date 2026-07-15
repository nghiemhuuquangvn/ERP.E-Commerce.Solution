using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IFileStorageSettingsService
    {
        Task<SystemFileStorageSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemFileStorageSettingsDto> UpdateSystemSettingsAsync(UpdateSystemFileStorageSettingsRequest request, CancellationToken ct);
        Task<TenantFileStorageSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantFileStorageSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantFileStorageSettingsRequest request, CancellationToken ct);
    }
}
