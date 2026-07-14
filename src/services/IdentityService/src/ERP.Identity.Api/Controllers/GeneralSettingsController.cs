using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// General settings — System (platform-wide) and Tenant (per-company) tiers,
    /// mirroring SettingsController's Auth Settings pattern. Storage+CRUD only for now.
    /// </summary>
    [ApiController]
    [Route("api/settings/general")]
    [Authorize]
    public class GeneralSettingsController : ControllerBase
    {
        private readonly IGeneralSettingsService _settingsService;
        private readonly IPermissionService _permissionService;

        public GeneralSettingsController(IGeneralSettingsService settingsService, IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _permissionService = permissionService;
        }

        [HttpGet("system")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(GeneralSettingsPermissions.SystemView)]
        public async Task<IActionResult> GetSystemSettings(CancellationToken ct = default)
        {
            var result = await _settingsService.GetSystemSettingsAsync(ct);
            return Ok(result);
        }

        [HttpPut("system")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(GeneralSettingsPermissions.SystemManage)]
        public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemGeneralSettingsRequest request, CancellationToken ct = default)
        {
            var result = await _settingsService.UpdateSystemSettingsAsync(request, ct);
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(GeneralSettingsPermissions.TenantView)]
        public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _settingsService.GetTenantSettingsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(GeneralSettingsPermissions.TenantManage)]
        public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantGeneralSettingsRequest request, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct);
            return Ok(result);
        }

        /// <summary>
        /// A caller may only manage a tenant's settings if the route tenantId matches their
        /// own JWT tenant_id claim, unless they also hold settings:system:manage (the same
        /// platform-SuperAdmin override SettingsController's Auth Settings already uses —
        /// deliberately not a General-specific override, so one SuperAdmin permission
        /// covers cross-tenant access to every settings category).
        /// </summary>
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
