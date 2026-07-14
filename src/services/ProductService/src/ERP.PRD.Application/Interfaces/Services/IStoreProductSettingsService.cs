using ERP.PRD.Application.DTOs.Settings;

namespace ERP.PRD.Application.Interfaces.Services;

public interface IStoreProductSettingsService
{
    Task<SystemStoreProductSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemStoreProductSettingsDto> UpdateSystemSettingsAsync(UpdateSystemStoreProductSettingsRequest request, CancellationToken ct);
    Task<TenantStoreProductSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantStoreProductSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantStoreProductSettingsRequest request, CancellationToken ct);
}
