using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Security;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// Management for service-to-service API keys — the "API Key management" gap
    /// from the business-analysis doc. The raw key is only ever returned once, in
    /// the Create response; wiring individual services' gateway routes to accept
    /// one (instead of/alongside a user Bearer token) is a separate, per-service
    /// follow-up, not part of this management surface.
    /// </summary>
    [ApiController]
    [Route("api/api-keys")]
    [Authorize]
    public class ApiKeysController : ControllerBase
    {
        private readonly IApiKeyService _apiKeyService;

        public ApiKeysController(IApiKeyService apiKeyService)
        {
            _apiKeyService = apiKeyService;
        }

        [HttpPost]
        [Authorize(Policy = "Permission")]
        [RequirePermission(Permissions.ManageApiKeys)]
        public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request, CancellationToken ct = default)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            var createdBy = User.FindFirst("sub")?.Value;
            var result = await _apiKeyService.CreateAsync(parsedTenantId, request.Name, request.Scopes, request.ExpiresInDays, createdBy, ct);
            return CreatedAtAction(nameof(List), new { }, result);
        }

        [HttpGet]
        [Authorize(Policy = "Permission")]
        [RequirePermission(Permissions.ViewApiKeys)]
        public async Task<IActionResult> List(CancellationToken ct = default)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
                return Unauthorized();

            var result = await _apiKeyService.ListByTenantAsync(parsedTenantId, ct);
            return Ok(result);
        }

        [HttpPost("{id:guid}/revoke")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(Permissions.ManageApiKeys)]
        public async Task<IActionResult> Revoke([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _apiKeyService.RevokeAsync(id, ct);
            return Ok(new { message = "API key revoked successfully" });
        }
    }
}
