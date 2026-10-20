using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Api.Authorization;
using ERP.Shared.Authorization;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ISettingsService _settingsService;
        private readonly ISessionService _sessionService;
        private readonly IPermissionService _permissionService;

        public UsersController(IUserService userService, ISettingsService settingsService, ISessionService sessionService, IPermissionService permissionService)
        {
            _userService = userService;
            _settingsService = settingsService;
            _sessionService = sessionService;
            _permissionService = permissionService;
        }

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Identity_UsersView)]
        [TenantRouteAccess(PermissionConstants.Identity_TenantsView)]
        public async Task<IActionResult> List([FromQuery] Guid? tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            // A different tenantId only gets here past [TenantRouteAccess] on the action
            // (platform-tenant caller holding identity:tenants:view).
            var effectiveTenantId = tenantId ?? callerTenantId.Value;

            var result = await _userService.ListByTenantAsync(effectiveTenantId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_UsersView)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            var user = await _userService.GetByIdAsync(id, callerTenantId.Value, ct);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        [HttpPut("{id}/profile")]
        [AuthorizePermission(PermissionConstants.Identity_UsersEdit)]
        public async Task<IActionResult> UpdateProfile([FromRoute] Guid id, [FromBody] UpdateProfileRequest request, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            await _userService.UpdateProfileAsync(id, callerTenantId.Value, request, ct);
            return Ok(new { message = "Profile updated successfully" });
        }

        [HttpPost("{id}/change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromRoute] Guid id, [FromBody] ChangePasswordRequest request, CancellationToken ct = default)
        {
            var callerUserId = this.GetCallerUserId();
            if (callerUserId is null || callerUserId.Value != id)
                return Forbid();

            await _userService.ChangePasswordAsync(id, request, ct);
            return Ok(new { message = "Password changed successfully" });
        }

        [HttpPost("{id}/2fa/enable")]
        [Authorize]
        public async Task<IActionResult> EnableTwoFactor([FromRoute] Guid id, [FromBody] EnableTwoFactorRequest request, CancellationToken ct = default)
        {
            var callerUserId = this.GetCallerUserId();
            if (callerUserId is null || callerUserId.Value != id)
                return Forbid();

            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            var tenantSettings = await _settingsService.GetTenantSettingsAsync(parsedTenantId, ct);
            if (!tenantSettings.TwoFactorEnabled)
                return BadRequest(new { message = "Two-factor authentication is disabled for this tenant." });

            await _userService.EnableTwoFactorAsync(id, request.Method, request.PhoneNumber, ct);
            return Ok(new { message = "Two-factor authentication enabled successfully" });
        }

        [HttpPost("{id}/2fa/disable")]
        [Authorize]
        public async Task<IActionResult> DisableTwoFactor([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerUserId = this.GetCallerUserId();
            if (callerUserId is null || callerUserId.Value != id)
                return Forbid();

            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            var tenantSettings = await _settingsService.GetTenantSettingsAsync(parsedTenantId, ct);
            if (tenantSettings.TwoFactorRequired)
                return BadRequest(new { message = "Two-factor authentication is mandatory for this tenant and cannot be disabled." });

            await _userService.DisableTwoFactorAsync(id, ct);
            return Ok(new { message = "Two-factor authentication disabled successfully" });
        }

        [HttpPost("{id}/lock")]
        [AuthorizePermission(PermissionConstants.Identity_UsersManage)]
        public async Task<IActionResult> Lock([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            await _userService.LockAsync(id, callerTenantId.Value, ct);
            return Ok(new { message = "User locked successfully" });
        }

        [HttpPost("{id}/unlock")]
        [AuthorizePermission(PermissionConstants.Identity_UsersManage)]
        public async Task<IActionResult> Unlock([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            await _userService.UnlockAsync(id, callerTenantId.Value, ct);
            return Ok(new { message = "User unlocked successfully" });
        }

        [HttpDelete("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_UsersDelete)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            await _userService.DeactivateAsync(id, callerTenantId.Value, ct);
            return Ok(new { message = "User deleted successfully" });
        }

        [HttpPost("{id}/roles")]
        [AuthorizePermission(PermissionConstants.Identity_UsersManage)]
        public async Task<IActionResult> AssignRole([FromRoute] Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct = default)
        {
            var parsedTenantId = this.GetCallerTenantId();
            if (parsedTenantId is null)
                return Unauthorized();

            await _userService.AssignRoleAsync(id, request.RoleCode, parsedTenantId.Value, this.GetCallerUserId(), this.GetCallerName(), ct);
            return Ok(new { message = "Role assigned successfully" });
        }

        [HttpDelete("{id}/roles/{roleId}")]
        [AuthorizePermission(PermissionConstants.Identity_UsersManage)]
        public async Task<IActionResult> RemoveRole([FromRoute] Guid id, [FromRoute] Guid roleId, CancellationToken ct = default)
        {
            var parsedTenantId = this.GetCallerTenantId();
            if (parsedTenantId is null)
                return Unauthorized();

            await _userService.RemoveRoleAsync(id, roleId, parsedTenantId.Value, ct);
            return Ok(new { message = "Role removed successfully" });
        }

        /// <summary>
        /// Attaches an already-existing account (found by email) to the caller's own
        /// tenant. Self-service registration deliberately does NOT do this silently
        /// (see AuthService.RegisterAsync) â€” only an authenticated admin action with
        /// manage_users may pull a known account into their tenant.
        /// </summary>
        [HttpPost("invite")]
        [AuthorizePermission(PermissionConstants.Identity_UsersManage)]
        public async Task<IActionResult> Invite([FromBody] InviteUserToTenantRequest request, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            var result = await _userService.InviteToTenantAsync(callerTenantId.Value, request, ct);
            return Ok(result);
        }

        /// <summary>"Where am I logged in" â€” lists this user's active DeviceSessions rows.</summary>
        [HttpGet("{id}/sessions")]
        [Authorize]
        public async Task<IActionResult> ListSessions([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerUserId = this.GetCallerUserId();
            if (callerUserId is null || callerUserId.Value != id)
                return Forbid();

            var sessions = await _sessionService.ListActiveSessionsAsync(id, ct);
            return Ok(sessions);
        }

        [HttpPost("{id}/sessions/{sessionId}/revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeSession([FromRoute] Guid id, [FromRoute] Guid sessionId, CancellationToken ct = default)
        {
            var callerUserId = this.GetCallerUserId();
            if (callerUserId is null || callerUserId.Value != id)
                return Forbid();

            await _sessionService.RevokeSessionAsync(id, sessionId, ct);
            return Ok(new { message = "Session revoked successfully" });
        }
    }
}
