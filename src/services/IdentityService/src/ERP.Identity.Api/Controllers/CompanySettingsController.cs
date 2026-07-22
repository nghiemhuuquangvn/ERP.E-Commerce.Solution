using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// Company settings — Tenant-only (no System tier; a company profile is inherently
    /// per-tenant). Storage+CRUD only for now.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/settings/company")]
    [Authorize]
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
        [Authorize(Policy = "Permission")]
        [RequirePermission(CompanySettingsPermissions.TenantView)]
        public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _settingsService.GetTenantSettingsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(CompanySettingsPermissions.TenantManage)]
        public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantCompanySettingsRequest request, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct);
            return Ok(result);
        }

        /// <summary>Same platform-SuperAdmin override as SettingsController/GeneralSettingsController.</summary>
        private async Task<bool> CallerCanAccessTenantAsync(Guid tenantId, CancellationToken ct)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId == tenantId)
                return true;

            var callerUserId = this.GetCallerUserId();
            if (callerUserId is null || callerTenantId is null)
                return false;

            return await _permissionService.HasPermissionAsync(callerUserId.Value, callerTenantId.Value, new[] { SettingsPermissions.SystemManage });
        }
    }
}
