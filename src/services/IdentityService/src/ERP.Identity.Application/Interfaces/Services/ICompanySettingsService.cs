using ERP.Identity.Application.DTOs.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface ICompanySettingsService
    {
        Task<TenantCompanySettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct);
        Task<TenantCompanySettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantCompanySettingsRequest request, CancellationToken ct);
    }
}
