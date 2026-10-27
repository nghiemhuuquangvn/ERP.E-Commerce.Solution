using ERP.SHP.Application.DTOs.Settings;

namespace ERP.SHP.Application.Interfaces;

public interface IShippingSettingsService
{
    Task<SystemShippingSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemShippingSettingsDto> UpdateSystemSettingsAsync(UpdateSystemShippingSettingsRequest request, CancellationToken ct);
    Task<TenantShippingSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantShippingSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantShippingSettingsRequest request, CancellationToken ct);

    /// <summary>The shop's account at each carrier (secrets never returned).</summary>
    Task<IReadOnlyList<CarrierAccountDto>> GetCarrierAccountsAsync(Guid tenantId, CancellationToken ct);
    Task<CarrierAccountDto> UpdateCarrierAccountAsync(Guid tenantId, string carrierCode, UpdateCarrierAccountRequest request, CancellationToken ct);
}
