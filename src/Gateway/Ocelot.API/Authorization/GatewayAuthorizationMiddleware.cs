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
    private readonly string _tenancyBaseDomain;

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
        "/api/v1/products",
        "/logs/api/v1/logs/client",
    ];

    // Anonymous auth endpoints that need to know which tenant they're operating on
    // before any JWT exists — resolved from the subdomain instead of a manual form
    // field (see login redesign, 2026-08). Register/forgot-password/reset-password/
    // confirm-email are deliberately excluded: those still take TenantCode explicitly
    // where relevant, unchanged by this middleware.
    //
    // Must match the real "/v1/"-versioned route paths (ocelot.json) exactly as a prefix —
    // these were written before that versioning pass and never updated, so this branch
    // never actually matched a single real request and tenant-from-subdomain resolution
    // was completely dead code until this fix.
    //
    // KNOWN FRAGILITY: this is a hand-maintained duplicate of the real route table
    // (ocelot.json + AuthController's [Route] templates) — the exact class of bug that
    // caused the dead-code incident above. Any future route/version change here needs the
    // same change made in lockstep in ocelot.json, with no compiler or runtime check to
    // catch a mismatch. Deriving this list from the route config instead of hand-listing it
    // would remove the failure mode entirely — flagged as tech debt, not fixed here.
    private static readonly string[] TenantResolvedAuthPathPrefixes =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/external-login",
        "/api/v1/auth/login-options",
    ];

    public GatewayAuthorizationMiddleware(RequestDelegate next, ILogger<GatewayAuthorizationMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _tenancyBaseDomain = configuration["Tenancy:BaseDomain"] ?? "erp.local";
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

                // Resolve the tenant from the subdomain (e.g. acme.erp.local -> "acme") for the
                // anonymous auth endpoints that need it before any JWT exists — replaces the old
                // manual "Tenant Code" login field for apps sharing one multi-tenant domain (Admin).
                // A client that already knows its own fixed tenant (e.g. Storefront, deployed
                // one-per-customer on its own domain, sending X-Tenant-Code from a build-time env
                // var) is trusted as-is and never overridden here — only requests that don't
                // already carry the header get subdomain resolution. Best-effort otherwise: if the
                // host doesn't carry a recognizable subdomain either (e.g. plain "erp.local" or an
                // unrelated domain), no header is set and the downstream controller rejects it.
                if (TenantResolvedAuthPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    && !context.Request.Headers.ContainsKey("X-Tenant-Code"))
                {
                    var tenantCode = ResolveTenantCodeFromHost(context.Request.Host.Host);
                    if (!string.IsNullOrEmpty(tenantCode))
                    {
                        context.Request.Headers["X-Tenant-Code"] = tenantCode;
                    }
                    else
                    {
                        _logger.LogWarning("Could not resolve tenant from host {Host} for {Path}", context.Request.Host.Host, path);
                    }
                }

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

    /// <summary>
    /// Extracts the leading label of <paramref name="host"/> as the tenant code, given it ends
    /// with "." + the configured base domain (e.g. host "acme.erp.local", base domain "erp.local"
    /// -> "acme"). Returns null for the bare base domain itself (no subdomain, no tenant) or any
    /// host that doesn't belong to the base domain at all.
    /// </summary>
    private string? ResolveTenantCodeFromHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        if (host.Equals(_tenancyBaseDomain, StringComparison.OrdinalIgnoreCase))
            return null;

        var suffix = "." + _tenancyBaseDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var tenantCode = host[..^suffix.Length];
        return string.IsNullOrWhiteSpace(tenantCode) ? null : tenantCode;
    }

    private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
        return dateTime;
    }
}
