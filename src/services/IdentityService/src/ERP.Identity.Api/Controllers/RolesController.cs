using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        [RequirePermission(Permissions.ViewRoles)]
        public async Task<IActionResult> List([FromQuery] Guid tenantId, CancellationToken ct = default)
        {
            var roles = await _roleService.ListByTenantAsync(tenantId, ct);
            return Ok(roles);
        }

        [HttpGet("{id}")]
        [RequirePermission(Permissions.ViewRoles)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct = default)
        {
            var role = await _roleService.GetByIdAsync(id, ct);
            if (role == null)
                return NotFound();
            return Ok(role);
        }

        [HttpPost]
        [RequirePermission(Permissions.CreateRoles)]
        public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken ct = default)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            var role = await _roleService.CreateAsync(parsedTenantId, request, ct);
            return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
        }

        [HttpPut("{id}")]
        [RequirePermission(Permissions.EditRoles)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct = default)
        {
            await _roleService.UpdateAsync(id, request, ct);
            return Ok(new { message = "Role updated successfully" });
        }

        [HttpDelete("{id}")]
        [RequirePermission(Permissions.DeleteRoles)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _roleService.DeleteAsync(id, ct);
            return Ok(new { message = "Role deleted successfully" });
        }

        [HttpGet("{id}/permissions")]
        [RequirePermission(Permissions.ViewRoles)]
        public async Task<IActionResult> GetPermissions([FromRoute] Guid id, CancellationToken ct = default)
        {
            var permissions = await _roleService.GetPermissionsAsync(id, ct);
            return Ok(permissions);
        }

        [HttpPost("{id}/permissions")]
        [RequirePermission(Permissions.ManageRoles)]
        public async Task<IActionResult> AssignPermission([FromRoute] Guid id, [FromBody] AssignPermissionToRoleRequest request, CancellationToken ct = default)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            await _roleService.AssignPermissionAsync(id, request.PermissionCode, parsedTenantId, ct);
            return Ok(new { message = "Permission assigned successfully" });
        }

        [HttpDelete("{id}/permissions/{permissionId}")]
        [RequirePermission(Permissions.ManageRoles)]
        public async Task<IActionResult> RemovePermission([FromRoute] Guid id, [FromRoute] Guid permissionId, CancellationToken ct = default)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            await _roleService.RemovePermissionAsync(id, permissionId, parsedTenantId, ct);
            return Ok(new { message = "Permission removed successfully" });
        }
    }
}
