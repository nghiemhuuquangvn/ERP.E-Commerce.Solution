using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Api.Authorization;
using ERP.Shared.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// Company settings â€” Tenant-only (no System tier; a company profile is inherently
    /// per-tenant). Storage+CRUD only for now.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/settings/company")]
    [Authorize]
    // Cross-tenant access (route/query tenantId ≠ caller's tenant) only for platform-tenant
    // callers holding settings:system:manage — see ITenantAccessPolicy.
    [TenantRouteAccess(PermissionConstants.Settings_SystemManage)]
    public class CompanySettingsController : ControllerBase
    {
        private readonly ICompanySettingsService _settingsService;
        private readonly IPermissionService _permissionService;

        public CompanySettingsController(ICompanySettingsService settingsService, IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _permissionService = permissionService;
        }

        [HttpGet("tenant/{tenantId}")]
        [AuthorizePermission(PermissionConstants.Settings_Company_TenantView)]
        public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            var result = await _settingsService.GetTenantSettingsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}")]
        [AuthorizePermission(PermissionConstants.Settings_Company_TenantManage)]
        public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantCompanySettingsRequest request, CancellationToken ct = default)
        {
            var result = await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct);
            return Ok(result);
        }

    }
}
