using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IWorkflowSettingsService
    {
        Task<SystemWorkflowSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemWorkflowSettingsDto> UpdateSystemSettingsAsync(UpdateSystemWorkflowSettingsRequest request, CancellationToken ct);
        Task<TenantWorkflowSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantWorkflowSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantWorkflowSettingsRequest request, CancellationToken ct);
    }
}
