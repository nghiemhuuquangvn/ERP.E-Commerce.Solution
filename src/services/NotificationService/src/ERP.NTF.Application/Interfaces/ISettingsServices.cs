using ERP.NTF.Application.DTOs.Settings;

namespace ERP.NTF.Application.Interfaces;

public interface INotificationSettingsService
{
    Task<SystemNotificationSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemNotificationSettingsDto> UpdateSystemSettingsAsync(UpdateSystemNotificationSettingsRequest request, CancellationToken ct);
    Task<TenantNotificationSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantNotificationSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantNotificationSettingsRequest request, CancellationToken ct);
}

public interface IEmailSettingsService
{
    Task<SystemEmailSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemEmailSettingsDto> UpdateSystemSettingsAsync(UpdateSystemEmailSettingsRequest request, CancellationToken ct);
    Task<TenantEmailSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantEmailSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantEmailSettingsRequest request, CancellationToken ct);
}

public interface ISmsSettingsService
{
    Task<SystemSmsSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
    Task<SystemSmsSettingsDto> UpdateSystemSettingsAsync(UpdateSystemSmsSettingsRequest request, CancellationToken ct);
    Task<TenantSmsSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
    Task<TenantSmsSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantSmsSettingsRequest request, CancellationToken ct);
}
