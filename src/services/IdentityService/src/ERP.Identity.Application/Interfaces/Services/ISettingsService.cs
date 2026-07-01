using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface ISettingsService
    {
        Task<SystemAuthSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemAuthSettingsDto> UpdateSystemSettingsAsync(UpdateSystemAuthSettingsRequest request, CancellationToken ct);
        Task<TenantAuthSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantAuthSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantAuthSettingsRequest request, CancellationToken ct);
        Task<PublicAuthOptionsDto> GetPublicOptionsAsync(string tenantCode, CancellationToken ct);
        Task<bool> IsProviderEnabledAsync(Guid tenantId, string provider, CancellationToken ct);

        /// <summary>
        /// Pure logic: true when 2FA is enabled system-wide AND for the tenant, AND
        /// either the tenant mandates it for everyone or the user opted in individually.
        /// </summary>
        bool IsTwoFactorRequiredForUser(User user, SystemAuthSettings systemSettings, TenantAuthSettings tenantSettings);

        /// <summary>
        /// Loads the singleton system settings entity, lazily creating+persisting the
        /// default row (via unit-of-work, not yet saved) if none exists.
        /// </summary>
        Task<SystemAuthSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct);

        /// <summary>
        /// Loads the tenant settings entity, lazily creating+persisting a default row
        /// (via unit-of-work, not yet saved) for pre-migration tenants that don't have one.
        /// </summary>
        Task<TenantAuthSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct);
    }
}
