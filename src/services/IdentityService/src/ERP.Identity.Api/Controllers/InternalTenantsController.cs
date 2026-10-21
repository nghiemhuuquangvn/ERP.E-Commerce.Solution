using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Shared.Authorization.ServiceAuth;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// Service-to-service tenant lookups (internal network only, service token required).
    /// </summary>
    [ApiController]
    [Route("internal/v1/tenants")]
    public class InternalTenantsController : ControllerBase
    {
        private readonly ITenantDomainService _tenantDomainService;

        public InternalTenantsController(ITenantDomainService tenantDomainService)
        {
            _tenantDomainService = tenantDomainService;
        }

        /// <summary>
        /// Host → tenant for the Gateway and ChatBot (TenantHostResolver). Tenant-less by
        /// nature: this is how the caller finds out which tenant a host belongs to.
        /// </summary>
        [HttpGet("resolve")]
        [AuthorizeInternalService("gateway", "chatbot", RequiresTenant = false)]
        public async Task<IActionResult> Resolve([FromQuery] string host, CancellationToken ct = default)
        {
            var resolution = await _tenantDomainService.ResolveAsync(host, ct);
            return resolution == null ? NotFound() : Ok(resolution);
        }
    }
}
