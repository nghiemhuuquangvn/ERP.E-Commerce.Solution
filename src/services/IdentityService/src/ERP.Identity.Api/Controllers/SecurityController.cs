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
        private readonly IPermissionService _permissionService;

        public SecurityController(ISecurityAuditService securityAuditService, IPermissionService permissionService)
        {
            _securityAuditService = securityAuditService;
            _permissionService = permissionService;
        }

        /// <summary>
        /// A caller without the platform-wide override may only ever see their own
        /// tenant's audit logs — any `tenantId` they pass (including none, which would
        /// otherwise mean "all tenants") is overridden to their own tenant. Fixes a
        /// cross-tenant data leak: this endpoint previously trusted the query
        /// parameter as-is with no ownership check at all.
        /// </summary>
        [HttpGet("audit-logs")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(Permissions.ViewAuditLogs)]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] Guid? tenantId, [FromQuery] Guid? userId, [FromQuery] string? eventType,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var effectiveTenantId = await ResolveEffectiveTenantIdAsync(tenantId, ct);
            if (effectiveTenantId is null && tenantId is not null)
                return Forbid();

            var result = await _securityAuditService.SearchAuditLogsAsync(effectiveTenantId, userId, eventType, page, pageSize, ct);
            return Ok(result);
        }

        /// <summary>
        /// LoginAttempts carries no TenantId at all (a failed login for a non-existent
        /// email never resolves to a tenant) — there is no per-tenant scoping possible,
        /// so this platform-wide view requires the SuperAdmin override permission, not
        /// just ViewAuditLogs, to avoid leaking every tenant's login activity (emails,
        /// IPs) to any tenant Admin who happens to hold ViewAuditLogs.
        /// </summary>
        [HttpGet("login-attempts")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(Permissions.ViewAuditLogs)]
        public async Task<IActionResult> GetLoginAttempts(
            [FromQuery] string? ipAddress, [FromQuery] string? email,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            if (!await CallerHasSystemOverrideAsync(ct))
                return Forbid();

            var result = await _securityAuditService.SearchLoginAttemptsAsync(ipAddress, email, page, pageSize, ct);
            return Ok(result);
        }

        /// <summary>
        /// Returns the tenantId the query should actually be scoped to: the caller's own
        /// tenant by default, the caller-supplied tenantId only if it matches their own
        /// tenant or they hold the SuperAdmin override, or null (meaning "all tenants",
        /// only ever reachable by an override holder who explicitly omitted tenantId).
        /// </summary>
        private async Task<Guid?> ResolveEffectiveTenantIdAsync(Guid? requestedTenantId, CancellationToken ct)
        {
            var callerTenantId = this.GetCallerTenantId();

            if (requestedTenantId is null)
                return await CallerHasSystemOverrideAsync(ct) ? null : callerTenantId;

            if (requestedTenantId == callerTenantId)
                return requestedTenantId;

            return await CallerHasSystemOverrideAsync(ct) ? requestedTenantId : null;
        }

        private async Task<bool> CallerHasSystemOverrideAsync(CancellationToken ct)
        {
            var callerUserId = this.GetCallerUserId();
            var callerTenantId = this.GetCallerTenantId();
            if (callerUserId is null || callerTenantId is null)
                return false;

            return await _permissionService.HasPermissionAsync(callerUserId.Value, callerTenantId.Value, new[] { SettingsPermissions.SystemManage });
        }
    }
}
