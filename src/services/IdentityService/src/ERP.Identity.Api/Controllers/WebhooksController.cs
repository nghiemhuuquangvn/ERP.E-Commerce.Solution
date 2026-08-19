using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Webhook;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Api.Authorization;
using ERP.Shared.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// Tenant-scoped CRUD for webhook subscriptions — see
    /// docs/business/Identity/CommonServiceRoadmap.md §3. Delivery itself happens from
    /// OutboxProcessor, not here; this controller only manages the endpoint catalog.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/webhooks")]
    [Authorize]
    public class WebhooksController : ControllerBase
    {
        private readonly IWebhookEndpointService _webhookEndpointService;

        public WebhooksController(IWebhookEndpointService webhookEndpointService)
        {
            _webhookEndpointService = webhookEndpointService;
        }

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Security_WebhooksView)]
        public async Task<IActionResult> List(CancellationToken ct = default)
        {
            var tenantId = this.GetCallerTenantId();
            if (tenantId is null)
                return Unauthorized();

            var result = await _webhookEndpointService.ListByTenantAsync(tenantId.Value, ct);
            return Ok(result);
        }

        [HttpPost]
        [AuthorizePermission(PermissionConstants.Security_WebhooksManage)]
        public async Task<IActionResult> Create([FromBody] CreateWebhookEndpointRequest request, CancellationToken ct = default)
        {
            var tenantId = this.GetCallerTenantId();
            if (tenantId is null)
                return Unauthorized();

            var result = await _webhookEndpointService.CreateAsync(tenantId.Value, request, ct);
            return CreatedAtAction(nameof(List), new { }, result);
        }

        [HttpPut("{id:guid}")]
        [AuthorizePermission(PermissionConstants.Security_WebhooksManage)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateWebhookEndpointRequest request, CancellationToken ct = default)
        {
            var tenantId = this.GetCallerTenantId();
            if (tenantId is null)
                return Unauthorized();

            await _webhookEndpointService.UpdateAsync(id, tenantId.Value, request, ct);
            return Ok(new { message = "Webhook endpoint updated successfully" });
        }

        [HttpPost("{id:guid}/activate")]
        [AuthorizePermission(PermissionConstants.Security_WebhooksManage)]
        public async Task<IActionResult> Activate([FromRoute] Guid id, CancellationToken ct = default)
        {
            var tenantId = this.GetCallerTenantId();
            if (tenantId is null)
                return Unauthorized();

            await _webhookEndpointService.ActivateAsync(id, tenantId.Value, ct);
            return Ok(new { message = "Webhook endpoint activated" });
        }

        [HttpPost("{id:guid}/deactivate")]
        [AuthorizePermission(PermissionConstants.Security_WebhooksManage)]
        public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct = default)
        {
            var tenantId = this.GetCallerTenantId();
            if (tenantId is null)
                return Unauthorized();

            await _webhookEndpointService.DeactivateAsync(id, tenantId.Value, ct);
            return Ok(new { message = "Webhook endpoint deactivated" });
        }

        [HttpDelete("{id:guid}")]
        [AuthorizePermission(PermissionConstants.Security_WebhooksManage)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct = default)
        {
            var tenantId = this.GetCallerTenantId();
            if (tenantId is null)
                return Unauthorized();

            await _webhookEndpointService.DeleteAsync(id, tenantId.Value, ct);
            return Ok(new { message = "Webhook endpoint deleted" });
        }
    }
}
