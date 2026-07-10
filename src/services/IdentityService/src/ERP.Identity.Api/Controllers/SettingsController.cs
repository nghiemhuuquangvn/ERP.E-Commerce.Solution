using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IPermissionService _permissionService;

        public SettingsController(ISettingsService settingsService, IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _permissionService = permissionService;
        }

        [HttpGet("system")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(SettingsPermissions.SystemView)]
        public async Task<IActionResult> GetSystemSettings(CancellationToken ct = default)
        {
            var result = await _settingsService.GetSystemSettingsAsync(ct);
            return Ok(result);
        }

        [HttpPut("system")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(SettingsPermissions.SystemManage)]
        public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemAuthSettingsRequest request, CancellationToken ct = default)
        {
            var result = await _settingsService.UpdateSystemSettingsAsync(request, ct);
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(SettingsPermissions.TenantView)]
        public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _settingsService.GetTenantSettingsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(SettingsPermissions.TenantManage)]
        public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantAuthSettingsRequest request, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct);
            return Ok(result);
        }

        /// <summary>
        /// A caller may only manage a tenant's settings if the route tenantId matches their
        /// own JWT tenant_id claim, unless they also hold settings:system:manage (cross-tenant override).
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
