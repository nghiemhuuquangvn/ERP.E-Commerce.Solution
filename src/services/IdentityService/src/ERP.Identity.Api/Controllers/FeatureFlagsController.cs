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
    /// Feature Flags — dynamic key-value settings, deliberately different shape from every
    /// other Settings category (admin-defined keys rather than fixed fields). Storage+CRUD
    /// only for now; nothing in the codebase reads these flags to gate behavior yet.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/settings/feature-flags")]
    [Authorize]
    public class FeatureFlagsController : ControllerBase
    {
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IPermissionService _permissionService;

        public FeatureFlagsController(IFeatureFlagService featureFlagService, IPermissionService permissionService)
        {
            _featureFlagService = featureFlagService;
            _permissionService = permissionService;
        }

        [HttpGet("system")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(FeatureFlagsPermissions.SystemView)]
        public async Task<IActionResult> GetSystemFlags(CancellationToken ct = default)
        {
            var result = await _featureFlagService.GetSystemFlagsAsync(ct);
            return Ok(result);
        }

        [HttpPut("system/{key}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(FeatureFlagsPermissions.SystemManage)]
        public async Task<IActionResult> UpsertSystemFlag([FromRoute] string key, [FromBody] UpsertSystemFeatureFlagRequest request, CancellationToken ct = default)
        {
            var result = await _featureFlagService.UpsertSystemFlagAsync(key, request, ct);
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(FeatureFlagsPermissions.TenantView)]
        public async Task<IActionResult> GetTenantFlags([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _featureFlagService.GetTenantFlagsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}/{key}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(FeatureFlagsPermissions.TenantManage)]
        public async Task<IActionResult> SetTenantFlagOverride([FromRoute] Guid tenantId, [FromRoute] string key, [FromBody] SetTenantFeatureFlagOverrideRequest request, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _featureFlagService.SetTenantFlagOverrideAsync(tenantId, key, request, ct);
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
