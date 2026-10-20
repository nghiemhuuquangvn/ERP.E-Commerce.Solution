using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Api.Authorization;
using ERP.Shared.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>Audit settings â€” System/Tenant tiers. Storage+CRUD only for now.</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/settings/audit")]
    [Authorize]
    // Cross-tenant access (route/query tenantId ≠ caller's tenant) only for platform-tenant
    // callers holding settings:system:manage — see ITenantAccessPolicy.
    [TenantRouteAccess(PermissionConstants.Settings_SystemManage)]
    public class AuditSettingsController : ControllerBase
    {
        private readonly IAuditSettingsService _settingsService;
        private readonly IPermissionService _permissionService;

        public AuditSettingsController(IAuditSettingsService settingsService, IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _permissionService = permissionService;
        }

        [HttpGet("system")]
        [AuthorizePermission(PermissionConstants.Settings_Audit_SystemView)]
        public async Task<IActionResult> GetSystemSettings(CancellationToken ct = default)
        {
            var result = await _settingsService.GetSystemSettingsAsync(ct);
            return Ok(result);
        }

        [HttpPut("system")]
        [AuthorizePermission(PermissionConstants.Settings_Audit_SystemManage)]
        public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemAuditSettingsRequest request, CancellationToken ct = default)
        {
            var result = await _settingsService.UpdateSystemSettingsAsync(request, ct);
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        [AuthorizePermission(PermissionConstants.Settings_Audit_TenantView)]
        public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            var result = await _settingsService.GetTenantSettingsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}")]
        [AuthorizePermission(PermissionConstants.Settings_Audit_TenantManage)]
        public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantAuditSettingsRequest request, CancellationToken ct = default)
        {
            var result = await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct);
            return Ok(result);
        }

    }
}
