using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Shared.Authorization.ServiceAuth;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// Service-to-service "who holds this permission" lookup (internal network only, service token required). NotificationService uses it to find
    /// the approvers of a purchase order, a sales order or a leave request; the tenant is the one the service token carries.
    /// </summary>
    [ApiController]
    [Route("internal/v1/permissions")]
    public class InternalPermissionsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public InternalPermissionsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>The distinct active users of the caller's tenant that hold <paramref name="code"/> (what their token carries).</summary>
        [HttpGet("{code}/users")]
        [AuthorizeInternalService("notification")]
        public async Task<IActionResult> UsersWithPermission([FromRoute] string code, CancellationToken ct = default)
        {
            // [AuthorizeInternalService] already refused a token without a tenant.
            if (!Guid.TryParse(User.FindFirst(ServiceAuthDefaults.TenantClaim)?.Value, out var tenantId) || tenantId == Guid.Empty)
                return BadRequest(new { message = "The service token carries no tenant." });

            var userIds = await _unitOfWork.Permissions.GetUserIdsWithPermissionAsync(code, tenantId, ct);
            return Ok(new PermissionHoldersResponse(code, userIds));
        }
    }

    public record PermissionHoldersResponse(string Permission, IReadOnlyList<Guid> UserIds);
}
