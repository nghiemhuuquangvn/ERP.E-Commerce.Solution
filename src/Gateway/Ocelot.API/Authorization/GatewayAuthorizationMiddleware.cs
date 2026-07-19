using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ERP.Shared.Authorization;

namespace Ocelot.API.Authorization;

/// <summary>
/// Middleware xử lý authentication và authorization tại API Gateway
/// - Validate JWT token
/// - Extract user info từ claims
/// - Tính toán permissions từ roles
/// - Thêm headers cho downstream services
/// </summary>
public class GatewayAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayAuthorizationMiddleware> _logger;

    // This middleware runs for every request regardless of whether the matched Ocelot
    // route actually requires Bearer auth (see ocelot.json — /api/auth/*, /api/products*,
    // and /logs/api/v1/logs/client (+ its /api/-prefixed mirror) are all deliberately
    // public), plus the Gateway's own /health used by K8s readiness/liveness probes every
    // 10-20s per pod. An unauthenticated request to any of these is expected, not
    // noteworthy — only log at Warning for paths outside this known-public set.
    private static readonly string[] KnownPublicPathPrefixes =
    [
        "/health",
        "/api/auth",
        "/api/products",
        "/logs/api/v1/logs/client",
        "/api/logs/api/v1/logs/client",
    ];

    public GatewayAuthorizationMiddleware(RequestDelegate next, ILogger<GatewayAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Check if user is authenticated (set by JWT authentication middleware)
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = ExtractClaim(context.User, "sub") ?? "unknown";
                // TokenService.GenerateAccessToken emits "name" (FullName/UserName/Email
                // fallback chain), not "preferred_username" — that claim never existed in
                // our own tokens, so this always fell back to the raw user GUID before.
                var userName = ExtractClaim(context.User, "name") ?? ExtractClaim(context.User, "preferred_username") ?? userId;
                var tenantId = ExtractClaim(context.User, "tenant_id");
                var tenantName = ExtractClaim(context.User, "tenant_name") ?? tenantId;
                var expiresAtStr = ExtractClaim(context.User, "exp");

                // Extract roles — identity service emits ClaimTypes.Role (full URI) as the JWT claim key,
                // so we must match by ClaimTypes.Role, not the short "role" name.
                var roles = context.User.FindAll(ClaimTypes.Role)
                    .Select(c => c.Value)
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                // Calculate permissions from roles
                var permissions = RoleDefinitions.GetPermissionsForRoles(roles);

                // Parse expiry
                var expiresAt = DateTime.UtcNow.AddHours(1);
                if (!string.IsNullOrEmpty(expiresAtStr) && long.TryParse(expiresAtStr, out var unixTime))
                {
                    expiresAt = UnixTimeStampToDateTime(unixTime);
                }

                _logger.LogInformation(
                    "User {UserId} from tenant {TenantId} with roles {Roles}",
                    userId, tenantId, string.Join(",", roles));

                // Add headers for downstream services
                context.Request.Headers.Add("X-User-Id", userId);
                context.Request.Headers.Add("X-User-Name", userName);

                if (!string.IsNullOrEmpty(tenantId))
                {
                    context.Request.Headers.Add("X-Tenant-Id", tenantId);
                }

                if (!string.IsNullOrEmpty(tenantName))
                {
                    context.Request.Headers.Add("X-Tenant-Name", tenantName);
                }

                if (roles.Length > 0)
                {
                    context.Request.Headers.Add("X-User-Roles", string.Join(",", roles));
                }

                if (permissions.Length > 0)
                {
                    context.Request.Headers.Add("X-User-Permissions", string.Join(",", permissions));
                }

                // Add expiry as ticks (HeaderUserContextService parses via long.TryParse → DateTime ticks)
                context.Request.Headers.Add("X-User-ExpiresAt", expiresAt.Ticks.ToString());

                _logger.LogInformation(
                    "Gateway headers set: UserId={UserId}, TenantId={TenantId}, Roles={Roles}, PermissionCount={PermissionCount}",
                    userId, tenantId, string.Join(",", roles), permissions.Length);
            }
            else
            {
                var path = context.Request.Path.Value ?? string.Empty;
                if (KnownPublicPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("Request without authentication (known public route: {Path})", path);
                }
                else
                {
                    _logger.LogWarning("Request without authentication ({Path})", path);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in gateway authorization middleware");
        }

        await _next(context);
    }

    private static string? ExtractClaim(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirst(claimType)?.Value;
    }

    private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
        return dateTime;
    }
}
