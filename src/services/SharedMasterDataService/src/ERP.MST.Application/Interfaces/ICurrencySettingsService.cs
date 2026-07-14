using ERP.MST.Application.DTOs.Settings;

namespace ERP.MST.Application.Interfaces;

public interface ICurrencySettingsService
{
    Task<SystemCurrencySettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemCurrencySettingsDto> UpdateSystemSettingsAsync(UpdateSystemCurrencySettingsRequest request, CancellationToken ct);
    Task<TenantCurrencySettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantCurrencySettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantCurrencySettingsRequest request, CancellationToken ct);
}
