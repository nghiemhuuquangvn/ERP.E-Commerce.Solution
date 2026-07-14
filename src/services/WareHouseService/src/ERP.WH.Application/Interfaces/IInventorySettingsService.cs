using ERP.WH.Application.DTOs.Settings;

namespace ERP.WH.Application.Interfaces;

public interface IInventorySettingsService
{
    Task<SystemInventorySettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemInventorySettingsDto> UpdateSystemSettingsAsync(UpdateSystemInventorySettingsRequest request, CancellationToken ct);
    Task<TenantInventorySettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantInventorySettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantInventorySettingsRequest request, CancellationToken ct);
}
