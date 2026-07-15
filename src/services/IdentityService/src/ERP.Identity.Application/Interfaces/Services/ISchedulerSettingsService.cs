using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface ISchedulerSettingsService
    {
        Task<SystemSchedulerSettingsDto> GetSystemSettingsAsync(CancellationToken ct);
        Task<SystemSchedulerSettingsDto> UpdateSystemSettingsAsync(UpdateSystemSchedulerSettingsRequest request, CancellationToken ct);
        Task<TenantSchedulerSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantSchedulerSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantSchedulerSettingsRequest request, CancellationToken ct);
    }
}
