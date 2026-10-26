using ERP.PAY.Application.DTOs.Settings;

namespace ERP.PAY.Application.Interfaces;

public interface IPaymentSettingsService
{
    Task<SystemPaymentSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemPaymentSettingsDto> UpdateSystemSettingsAsync(UpdateSystemPaymentSettingsRequest request, CancellationToken ct);
    Task<TenantPaymentSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantPaymentSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantPaymentSettingsRequest request, CancellationToken ct);
    Task<IReadOnlyList<GatewayCredentialDto>> GetGatewaysAsync(Guid tenantId, CancellationToken ct);
    Task<GatewayCredentialDto> UpdateGatewayAsync(Guid tenantId, string provider, UpdateGatewayCredentialRequest request, CancellationToken ct);
}
