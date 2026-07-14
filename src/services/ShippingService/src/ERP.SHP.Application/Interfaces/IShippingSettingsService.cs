using ERP.SHP.Application.DTOs.Settings;

namespace ERP.SHP.Application.Interfaces;

public interface IShippingSettingsService
{
    Task<SystemShippingSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemShippingSettingsDto> UpdateSystemSettingsAsync(UpdateSystemShippingSettingsRequest request, CancellationToken ct);
    Task<TenantShippingSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantShippingSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantShippingSettingsRequest request, CancellationToken ct);
}
