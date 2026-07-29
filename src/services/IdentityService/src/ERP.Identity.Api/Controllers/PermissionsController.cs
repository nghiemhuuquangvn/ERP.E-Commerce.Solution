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
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public PermissionsController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Identity_PermissionsView)]
        public async Task<IActionResult> List(CancellationToken ct = default)
        {
            var permissions = await _permissionService.ListAllAsync(ct);
            return Ok(permissions);
        }

        [HttpPost]
        [AuthorizePermission(PermissionConstants.Identity_PermissionsManage)]
        public async Task<IActionResult> Create([FromBody] CreatePermissionRequest request, CancellationToken ct = default)
        {
            var permission = await _permissionService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(List), permission);
        }

        [HttpPut("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_PermissionsManage)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdatePermissionRequest request, CancellationToken ct = default)
        {
            await _permissionService.UpdateAsync(id, request, ct);
            return Ok(new { message = "Permission updated successfully" });
        }

        [HttpDelete("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_PermissionsManage)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _permissionService.DeleteAsync(id, ct);
            return Ok(new { message = "Permission deleted successfully" });
        }
    }
}
