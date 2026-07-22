using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>Localization settings — System/Tenant tiers. Storage+CRUD only for now.</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/settings/localization")]
    [Authorize]
    public class LocalizationSettingsController : ControllerBase
    {
        private readonly ILocalizationSettingsService _settingsService;
        private readonly IPermissionService _permissionService;

        public LocalizationSettingsController(ILocalizationSettingsService settingsService, IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _permissionService = permissionService;
        }

        [HttpGet("system")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LocalizationSettingsPermissions.SystemView)]
        public async Task<IActionResult> GetSystemSettings(CancellationToken ct = default)
        {
            var result = await _settingsService.GetSystemSettingsAsync(ct);
            return Ok(result);
        }

        [HttpPut("system")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LocalizationSettingsPermissions.SystemManage)]
        public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemLocalizationSettingsRequest request, CancellationToken ct = default)
        {
            var result = await _settingsService.UpdateSystemSettingsAsync(request, ct);
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LocalizationSettingsPermissions.TenantView)]
        public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _settingsService.GetTenantSettingsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LocalizationSettingsPermissions.TenantManage)]
        public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantLocalizationSettingsRequest request, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct);
            return Ok(result);
        }

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
