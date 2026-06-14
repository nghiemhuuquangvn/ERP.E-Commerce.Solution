using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [RequirePermission(Permissions.ViewUsers)]
        public async Task<IActionResult> List([FromQuery] Guid tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var result = await _userService.ListByTenantAsync(tenantId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [RequirePermission(Permissions.ViewUsers)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct = default)
        {
            var user = await _userService.GetByIdAsync(id, ct);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        [HttpPut("{id}/profile")]
        [RequirePermission(Permissions.EditUsers)]
        public async Task<IActionResult> UpdateProfile([FromRoute] Guid id, [FromBody] UpdateProfileRequest request, CancellationToken ct = default)
        {
            await _userService.UpdateProfileAsync(id, request, ct);
            return Ok(new { message = "Profile updated successfully" });
        }

        [HttpPost("{id}/change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromRoute] Guid id, [FromBody] ChangePasswordRequest request, CancellationToken ct = default)
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId) || parsedUserId != id)
                return Forbid();

            await _userService.ChangePasswordAsync(id, request, ct);
            return Ok(new { message = "Password changed successfully" });
        }

        [HttpPost("{id}/lock")]
        [RequirePermission(Permissions.ManageUsers)]
        public async Task<IActionResult> Lock([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _userService.LockAsync(id, ct);
            return Ok(new { message = "User locked successfully" });
        }

        [HttpPost("{id}/unlock")]
        [RequirePermission(Permissions.ManageUsers)]
        public async Task<IActionResult> Unlock([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _userService.UnlockAsync(id, ct);
            return Ok(new { message = "User unlocked successfully" });
        }

        [HttpDelete("{id}")]
        [RequirePermission(Permissions.DeleteUsers)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _userService.DeactivateAsync(id, ct);
            return Ok(new { message = "User deleted successfully" });
        }

        [HttpPost("{id}/roles")]
        [RequirePermission(Permissions.ManageUsers)]
        public async Task<IActionResult> AssignRole([FromRoute] Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct = default)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            await _userService.AssignRoleAsync(id, request.RoleCode, parsedTenantId, ct);
            return Ok(new { message = "Role assigned successfully" });
        }

        [HttpDelete("{id}/roles/{roleId}")]
        [RequirePermission(Permissions.ManageUsers)]
        public async Task<IActionResult> RemoveRole([FromRoute] Guid id, [FromRoute] Guid roleId, CancellationToken ct = default)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            await _userService.RemoveRoleAsync(id, roleId, parsedTenantId, ct);
            return Ok(new { message = "Role removed successfully" });
        }
    }
}
