using ERP.PAY.Application.DTOs.Settings;

namespace ERP.PAY.Application.Interfaces;

public interface IPaymentSettingsService
{
    Task<SystemPaymentSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemPaymentSettingsDto> UpdateSystemSettingsAsync(UpdateSystemPaymentSettingsRequest request, CancellationToken ct);
    Task<TenantPaymentSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantPaymentSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantPaymentSettingsRequest request, CancellationToken ct);
}
