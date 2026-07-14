using ERP.ACC.Application.DTOs.Settings;

namespace ERP.ACC.Application.Interfaces;

public interface ITaxSettingsService
{
    Task<SystemTaxSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemTaxSettingsDto> UpdateSystemSettingsAsync(UpdateSystemTaxSettingsRequest request, CancellationToken ct);
    Task<TenantTaxSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantTaxSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantTaxSettingsRequest request, CancellationToken ct);
}
