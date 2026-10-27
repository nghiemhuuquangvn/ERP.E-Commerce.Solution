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
    // route actually requires Bearer auth (see ocelot.json — /api/v1/auth/*,
    // /api/v1/products*, and /logs/api/v1/logs/client are all deliberately public), plus
    // the Gateway's own /health used by K8s readiness/liveness probes every 10-20s per pod.
    // An unauthenticated request to any of these is expected, not noteworthy — only log at
    // Warning for paths outside this known-public set. Kept in sync with the actual
    // "/v1/"-versioned routes in ocelot.json — these previously lacked "/v1/" and silently
    // never matched real requests (harmless here since it only affects log level), and the
    // double-prefix "/api/logs/api/v1/logs/client" mirror this used to also list no longer
    // exists at all (removed as dead weight once the frontend stopped double-prefixing).
    private static readonly string[] KnownPublicPathPrefixes =
    [
        "/health",
        "/api/v1/auth",
        "/api/v1/payment-gateway/callback",   // gateway notifications and the browser return: signed, no bearer token
        "/api/v1/payment-gateway/ipn",
        "/pay/api/v1/payment-gateway/callback",
        "/pay/api/v1/payment-gateway/ipn",
        "/api/v1/shipping-webhooks",   // carrier notifications: authenticated by the shop's shared secret, no bearer token
        "/shp/api/v1/shipping-webhooks",
        "/api/v1/products",
        "/logs/api/v1/logs/client",
    ];


    public GatewayAuthorizationMiddleware(RequestDelegate next, ILogger<GatewayAuthorizationMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
    }

    // Headers only the Gateway may set. Anything a client sends under these names is
    // dropped before routing, so nothing downstream can be fed a forged tenant/user.
    private static readonly string[] GatewayOwnedHeaders =
    [
        "X-Tenant-Code", "X-Tenant-Id", "X-Tenant-Name",
        "X-User-Id", "X-User-Name", "X-User-Roles", "X-User-Permissions", "X-User-ExpiresAt",
    ];

    public async Task InvokeAsync(HttpContext context, TenantHostResolver tenantResolver)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        foreach (var header in GatewayOwnedHeaders)
            context.Request.Headers.Remove(header);

        // Probes hit the pod IP, not a tenant host. The service-token JWKS is fetched by
        // IdentityService over the internal network to verify the Gateway's own host lookups —
        // resolving a tenant for it would call Identity again (a loop). Public keys only.
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, ERP.Shared.Authorization.ServiceAuth.ServiceAuthDefaults.JwksPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 1) The tenant is decided by the URL (custom domain, <code>.<base-domain>, or the
        //    platform host) — never by the client. Unknown host = no tenant = no service.
        var tenant = await tenantResolver.ResolveAsync(context.Request.Host.Host, context.RequestAborted);
        if (tenant is null)
        {
            _logger.LogWarning("Rejected request for unknown tenant host {Host} ({Path})", context.Request.Host.Host, path);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { message = "Unknown tenant host." });
            return;
        }

        context.Request.Headers["X-Tenant-Code"] = tenant.TenantCode;
        context.Request.Headers["X-Tenant-Id"] = tenant.TenantId.ToString();

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            // 2) A token is only valid on its own tenant's host: a tenant-A token on tenant B's
            //    domain (or a platform token on a tenant domain) is rejected outright.
            var tokenTenantId = ExtractClaim(context.User, "tenant_id");
            if (!Guid.TryParse(tokenTenantId, out var tokenTenant) || tokenTenant != tenant.TenantId)
            {
                _logger.LogWarning("Rejected token of tenant {TokenTenant} on host {Host} (tenant {HostTenant})",
                    tokenTenantId, context.Request.Host.Host, tenant.TenantId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Token does not belong to this tenant." });
                return;
            }

            AddUserHeaders(context);
        }
        else if (!KnownPublicPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Request without authentication ({Path})", path);
        }

        await _next(context);
    }

    /// <summary>Informational user headers for downstream logging — every service still
    /// validates the JWT itself and never trusts these for authorization.</summary>
    private void AddUserHeaders(HttpContext context)
    {
        var userId = ExtractClaim(context.User, "sub") ?? "unknown";
        var userName = ExtractClaim(context.User, "name") ?? ExtractClaim(context.User, "preferred_username") ?? userId;
        var roles = context.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var permissions = context.User.FindAll("permission")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expiresAt = DateTime.UtcNow.AddHours(1);
        if (long.TryParse(ExtractClaim(context.User, "exp"), out var unixTime))
            expiresAt = UnixTimeStampToDateTime(unixTime);

        context.Request.Headers["X-User-Id"] = userId;
        context.Request.Headers["X-User-Name"] = userName;
        var tenantName = ExtractClaim(context.User, "tenant_name");
        if (!string.IsNullOrEmpty(tenantName))
            context.Request.Headers["X-Tenant-Name"] = tenantName;
        if (roles.Length > 0)
            context.Request.Headers["X-User-Roles"] = string.Join(",", roles);
        if (permissions.Length > 0)
            context.Request.Headers["X-User-Permissions"] = string.Join(",", permissions);
        context.Request.Headers["X-User-ExpiresAt"] = expiresAt.Ticks.ToString();
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
