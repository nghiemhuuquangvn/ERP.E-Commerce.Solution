using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// Read-side for SecurityAuditLogs/LoginAttempts — the two tables that existed in
    /// 001_CreateAuthDB.sql with no corresponding C# entity or endpoint until now.
    /// </summary>
    [ApiController]
    [Route("api/security")]
    [Authorize]
    public class SecurityController : ControllerBase
    {
        private readonly ISecurityAuditService _securityAuditService;

        public SecurityController(ISecurityAuditService securityAuditService)
        {
            _securityAuditService = securityAuditService;
        }

        [HttpGet("audit-logs")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(Permissions.ViewAuditLogs)]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] Guid? tenantId, [FromQuery] Guid? userId, [FromQuery] string? eventType,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _securityAuditService.SearchAuditLogsAsync(tenantId, userId, eventType, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("login-attempts")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(Permissions.ViewAuditLogs)]
        public async Task<IActionResult> GetLoginAttempts(
            [FromQuery] string? ipAddress, [FromQuery] string? email,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _securityAuditService.SearchLoginAttemptsAsync(ipAddress, email, page, pageSize, ct);
            return Ok(result);
        }
    }
}
