using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Api.Authorization;
using ERP.Shared.Authorization;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    // Cross-tenant access (route/query tenantId ≠ caller's tenant) only for platform-tenant
    // callers holding settings:system:manage — see ITenantAccessPolicy.
    [TenantRouteAccess(PermissionConstants.Settings_SystemManage)]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IPermissionService _permissionService;
        private readonly IUserService _userService;

        public SettingsController(ISettingsService settingsService, IPermissionService permissionService, IUserService userService)
        {
            _settingsService = settingsService;
            _permissionService = permissionService;
            _userService = userService;
        }

        [HttpGet("system")]
        [AuthorizePermission(PermissionConstants.Settings_SystemView)]
        public async Task<IActionResult> GetSystemSettings(CancellationToken ct = default)
        {
            var result = await _settingsService.GetSystemSettingsAsync(ct);
            return Ok(result);
        }

        [HttpPut("system")]
        [AuthorizePermission(PermissionConstants.Settings_SystemManage)]
        public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemAuthSettingsRequest request, CancellationToken ct = default)
        {
            if (await ConfirmCallerAsync(request.CurrentPassword, ct) is { } refusal) return refusal;
            var result = await _settingsService.UpdateSystemSettingsAsync(request, ct);
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        [AuthorizePermission(PermissionConstants.Settings_TenantView)]
        public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct = default)
        {
            var result = await _settingsService.GetTenantSettingsAsync(tenantId, ct);
            return Ok(result);
        }

        [HttpPut("tenant/{tenantId}")]
        [AuthorizePermission(PermissionConstants.Settings_TenantManage)]
        public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantAuthSettingsRequest request, CancellationToken ct = default)
        {
            if (await ConfirmCallerAsync(request.CurrentPassword, ct) is { } refusal) return refusal;
            var result = await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct);
            return Ok(result);
        }

        /// <summary>
        /// SEC-M6 (BR-BF-08): authentication settings decide 2FA, lockout and sessions for a whole shop - one stolen
        /// admin session must not be enough to weaken them. The caller re-confirms (password, or a fresh external
        /// sign-in); a refusal is the business-rule 400 from IUserService.ConfirmCallerAsync.
        /// </summary>
        private async Task<IActionResult?> ConfirmCallerAsync(string? currentPassword, CancellationToken ct)
        {
            var callerId = this.GetCallerUserId();
            if (callerId is null)
                return Unauthorized();
            DateTimeOffset? issuedAt = long.TryParse(User.FindFirst("iat")?.Value, out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
            await _userService.ConfirmCallerAsync(callerId.Value, "AuthSettingsChange", currentPassword, issuedAt, ct);
            return null;
        }
    }
}
