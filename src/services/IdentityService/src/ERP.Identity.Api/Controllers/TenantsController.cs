using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Tenant;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Api.Authorization;
using ERP.Shared.Authorization;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;

        public TenantsController(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Identity_TenantsView)]
        public async Task<IActionResult> List(CancellationToken ct = default)
        {
            var tenants = await _tenantService.ListAllAsync(ct);
            return Ok(tenants);
        }

        [HttpGet("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_TenantsView)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct = default)
        {
            var tenant = await _tenantService.GetByIdAsync(id, ct);
            if (tenant == null)
                return NotFound();
            return Ok(tenant);
        }

        [HttpPost]
        [AuthorizePermission(PermissionConstants.Identity_TenantsCreate)]
        public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken ct = default)
        {
            var tenant = await _tenantService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
        }

        [HttpPut("{id}")]
        [AuthorizePermission(PermissionConstants.Identity_TenantsEdit)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct = default)
        {
            await _tenantService.UpdateAsync(id, request, ct);
            return Ok(new { message = "Tenant updated successfully" });
        }

        [HttpPost("{id}/activate")]
        [AuthorizePermission(PermissionConstants.Identity_TenantsManage)]
        public async Task<IActionResult> Activate([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _tenantService.ActivateAsync(id, ct);
            return Ok(new { message = "Tenant activated successfully" });
        }

        [HttpPost("{id}/deactivate")]
        [AuthorizePermission(PermissionConstants.Identity_TenantsManage)]
        public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _tenantService.DeactivateAsync(id, ct);
            return Ok(new { message = "Tenant deactivated successfully" });
        }
    }
}
