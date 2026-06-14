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
                var userName = ExtractClaim(context.User, "preferred_username") ?? userId;
                var tenantId = ExtractClaim(context.User, "tenant_id");
                var tenantName = ExtractClaim(context.User, "tenant_name") ?? tenantId;
                var expiresAtStr = ExtractClaim(context.User, "exp");

                // Extract roles
                var roles = context.User.FindAll("role")
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

                // Add expiry as unix timestamp
                context.Request.Headers.Add("X-User-ExpiresAt", expiresAt.Ticks.ToString());

                _logger.LogInformation(
                    "Gateway headers set: UserId={UserId}, TenantId={TenantId}, Roles={Roles}, PermissionCount={PermissionCount}",
                    userId, tenantId, string.Join(",", roles), permissions.Length);
            }
            else
            {
                _logger.LogWarning("Request without authentication");
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
