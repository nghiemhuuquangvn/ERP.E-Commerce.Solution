using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.License;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// Per-tenant license lifecycle (issue/renew/suspend/revoke), commercially controlled —
    /// deliberately NOT a self-service Settings category. GET follows the usual "own tenant
    /// OR SystemManage override" rule (FeatureFlagsController/SecurityController pattern) so
    /// a tenant Admin can see their own license read-only. The lifecycle actions are
    /// SuperAdmin-only in practice: the migration grants license:tenant:issue to the
    /// SuperAdmin role alone, and — unlike every other Settings category — even a caller
    /// who somehow holds that permission for their OWN tenant is still rejected unless they
    /// also hold the cross-tenant SystemManage override, i.e. there is no same-tenant
    /// self-service shortcut here at all.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/license/tenant")]
    [Authorize]
    public class TenantLicensesController : ControllerBase
    {
        private readonly ILicenseService _licenseService;
        private readonly IPermissionService _permissionService;

        public TenantLicensesController(ILicenseService licenseService, IPermissionService permissionService)
        {
            _licenseService = licenseService;
            _permissionService = permissionService;
        }

        [HttpGet("{tenantId}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LicensePermissions.TenantView)]
        public async Task<IActionResult> GetTenantLicense([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerCanAccessTenantAsync(tenantId, ct))
                return Forbid();

            var result = await _licenseService.GetTenantLicenseAsync(tenantId, ct);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost("{tenantId}/issue")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LicensePermissions.TenantIssue)]
        public async Task<IActionResult> IssueLicense([FromRoute] Guid tenantId, [FromBody] IssueLicenseRequest request, CancellationToken ct = default)
        {
            if (!await CallerHasSystemOverrideAsync(ct))
                return Forbid();

            var issuedByUserId = this.GetCallerUserId();
            if (issuedByUserId is null)
                return Forbid();

            var result = await _licenseService.IssueLicenseAsync(tenantId, request, issuedByUserId.Value.ToString(), ct);
            return Ok(result);
        }

        [HttpPost("{tenantId}/renew")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LicensePermissions.TenantIssue)]
        public async Task<IActionResult> RenewLicense([FromRoute] Guid tenantId, [FromBody] RenewLicenseRequest request, CancellationToken ct = default)
        {
            if (!await CallerHasSystemOverrideAsync(ct))
                return Forbid();

            var result = await _licenseService.RenewLicenseAsync(tenantId, request, ct);
            return Ok(result);
        }

        [HttpPost("{tenantId}/suspend")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LicensePermissions.TenantIssue)]
        public async Task<IActionResult> SuspendLicense([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerHasSystemOverrideAsync(ct))
                return Forbid();

            await _licenseService.SuspendLicenseAsync(tenantId, ct);
            return NoContent();
        }

        [HttpPost("{tenantId}/revoke")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LicensePermissions.TenantIssue)]
        public async Task<IActionResult> RevokeLicense([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            if (!await CallerHasSystemOverrideAsync(ct))
                return Forbid();

            await _licenseService.RevokeLicenseAsync(tenantId, ct);
            return NoContent();
        }

        private async Task<bool> CallerCanAccessTenantAsync(Guid tenantId, CancellationToken ct)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId == tenantId)
                return true;

            return await CallerHasSystemOverrideAsync(ct);
        }

        private async Task<bool> CallerHasSystemOverrideAsync(CancellationToken ct)
        {
            var callerUserId = this.GetCallerUserId();
            var callerTenantId = this.GetCallerTenantId();
            if (callerUserId is null || callerTenantId is null)
                return false;

            return await _permissionService.HasPermissionAsync(callerUserId.Value, callerTenantId.Value, new[] { SettingsPermissions.SystemManage });
        }
    }
}
