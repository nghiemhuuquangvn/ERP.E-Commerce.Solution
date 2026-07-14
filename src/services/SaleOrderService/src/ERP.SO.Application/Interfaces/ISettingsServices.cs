using ERP.SO.Application.DTOs.Settings;

namespace ERP.SO.Application.Interfaces;

public interface ICartCheckoutSettingsService
{
    Task<SystemCartCheckoutSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemCartCheckoutSettingsDto> UpdateSystemSettingsAsync(UpdateSystemCartCheckoutSettingsRequest request, CancellationToken ct);
    Task<TenantCartCheckoutSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantCartCheckoutSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantCartCheckoutSettingsRequest request, CancellationToken ct);
}

public interface IOrderSettingsService
{
    Task<SystemOrderSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemOrderSettingsDto> UpdateSystemSettingsAsync(UpdateSystemOrderSettingsRequest request, CancellationToken ct);
    Task<TenantOrderSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantOrderSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantOrderSettingsRequest request, CancellationToken ct);
}
