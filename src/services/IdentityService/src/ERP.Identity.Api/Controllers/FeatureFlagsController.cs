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
    /// Feature Flags â€” dynamic key-value settings, deliberately different shape from every
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
        [AuthorizePermission(PermissionConstants.Settings_FeatureFlags_SystemView)]
        public async Task<IActionResult> GetSystemFlags(CancellationToken ct = default)
        {
            var result = await _featureFlagService.GetSystemFlagsAsync(ct);
            return Ok(result);
        }

        [HttpPut("system/{key}")]
        [AuthorizePermission(PermissionConstants.Settings_FeatureFlags_SystemManage)]
        public async Task<IActionResult> UpsertSystemFlag([FromRoute] string key, [FromBody] UpsertSystemFeatureFlagRequest request, CancellationToken ct = default)
        {
            var result = await _featureFlagService.UpsertSystemFlagAsync(key, request, ct);
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        [AuthorizePermission(PermissionConstants.Settings_FeatureFlags_TenantView)]
        public async Task<IActionResult> GetTenantFlags([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _featureFlagService.GetTenantFlagsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}/{key}")]
        [AuthorizePermission(PermissionConstants.Settings_FeatureFlags_TenantManage)]
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

            return await _permissionService.HasPermissionAsync(callerUserId.Value, callerTenantId.Value, new[] { PermissionConstants.Settings_SystemManage });
        }
    }
}
