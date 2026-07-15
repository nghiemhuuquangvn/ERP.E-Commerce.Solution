using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IAuditSettingsService
    {
        Task<SystemAuditSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemAuditSettingsDto> UpdateSystemSettingsAsync(UpdateSystemAuditSettingsRequest request, CancellationToken ct);
        Task<TenantAuditSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantAuditSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantAuditSettingsRequest request, CancellationToken ct);
    }
}
