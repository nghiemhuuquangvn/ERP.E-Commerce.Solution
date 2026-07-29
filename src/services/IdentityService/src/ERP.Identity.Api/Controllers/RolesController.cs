using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly IPermissionService _permissionService;

        public RolesController(IRoleService roleService, IPermissionService permissionService)
        {
            _roleService = roleService;
            _permissionService = permissionService;
        }

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Identity_RolesView)]
        public async Task<IActionResult> List([FromQuery] Guid? tenantId, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            var effectiveTenantId = callerTenantId.Value;

            if (tenantId.HasValue && tenantId.Value != callerTenantId.Value)
            {
                // Same cross-tenant override as UsersController.List â€” only callers
                // holding identity:tenants:view (re-verified against the DB) may view
                // another tenant's roles.
                var callerUserId = this.GetCallerUserId();
                if (callerUserId is null)
                    return Unauthorized();

                var canCrossTenant = await _permissionService.HasPermissionAsync(
                    callerUserId.Value, callerTenantId.Value, new[] { PermissionConstants.Identity_TenantsView });
                if (!canCrossTenant)
                    return Forbid();

                effectiveTenantId = tenantId.Value;
            }

            var roles = await _roleService.ListByTenantAsync(effectiveTenantId, ct);
            return Ok(roles);
        }

        [HttpGet("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_RolesView)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            var role = await _roleService.GetByIdAsync(id, callerTenantId.Value, ct);
            if (role == null)
                return NotFound();
            return Ok(role);
        }

        [HttpPost]
        [AuthorizePermission(PermissionConstants.Identity_RolesCreate)]
        public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken ct = default)
        {
            var tenantId = this.GetCallerTenantId();
            if (tenantId is null)
                return Unauthorized();

            var role = await _roleService.CreateAsync(tenantId.Value, request, ct);
            return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
        }

        [HttpPut("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_RolesEdit)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            await _roleService.UpdateAsync(id, callerTenantId.Value, request, ct);
            return Ok(new { message = "Role updated successfully" });
        }

        [HttpDelete("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_RolesDelete)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            await _roleService.DeleteAsync(id, callerTenantId.Value, ct);
            return Ok(new { message = "Role deleted successfully" });
        }

        [HttpGet("{id}/permissions")]
        [AuthorizePermission(PermissionConstants.Identity_RolesView)]
        public async Task<IActionResult> GetPermissions([FromRoute] Guid id, CancellationToken ct = default)
        {
            var callerTenantId = this.GetCallerTenantId();
            if (callerTenantId is null)
                return Unauthorized();

            var permissions = await _roleService.GetPermissionsAsync(id, callerTenantId.Value, ct);
            return Ok(permissions);
        }

        [HttpPost("{id}/permissions")]
        [AuthorizePermission(PermissionConstants.Identity_RolesManage)]
        public async Task<IActionResult> AssignPermission([FromRoute] Guid id, [FromBody] AssignPermissionToRoleRequest request, CancellationToken ct = default)
        {
            var parsedTenantId = this.GetCallerTenantId();
            if (parsedTenantId is null)
                return Unauthorized();

            await _roleService.AssignPermissionAsync(id, request.PermissionCode, parsedTenantId.Value, ct);
            return Ok(new { message = "Permission assigned successfully" });
        }

        [HttpDelete("{id}/permissions/{permissionId}")]
        [AuthorizePermission(PermissionConstants.Identity_RolesManage)]
        public async Task<IActionResult> RemovePermission([FromRoute] Guid id, [FromRoute] Guid permissionId, CancellationToken ct = default)
        {
            var parsedTenantId = this.GetCallerTenantId();
            if (parsedTenantId is null)
                return Unauthorized();

            await _roleService.RemovePermissionAsync(id, permissionId, parsedTenantId.Value, ct);
            return Ok(new { message = "Permission removed successfully" });
        }
    }
}
