using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ERP.Shared.Authorization;

/// <summary>
/// Builds UserContext from the current request's validated JWT claims
/// (HttpContext.User, populated by AddErpJwtAuthentication + app.UseAuthentication())
/// instead of trusting the Gateway's forwarded X-User-* headers. This is what actually
/// closes the cross-service impersonation hole — HeaderUserContextService is kept
/// only for services/paths that legitimately never see an Authorization header (none,
/// today, but left in place as the pre-existing fallback implementation rather than
/// deleted).
/// </summary>
public class JwtClaimsUserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtClaimsUserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public UserContext GetCurrentUser()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return new UserContext();

        // "sub"/"email" are remapped to their long ClaimTypes URI equivalents by the
        // default JWT inbound claim mapping unless MapInboundClaims=false is set on the
        // handler — check both forms so this works regardless of that setting.
        var userId = FindFirst(user, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        var email = FindFirst(user, JwtRegisteredClaimNames.Email, ClaimTypes.Email);
        var tenantId = FindFirst(user, "tenant_id");
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var permissions = user.FindAll("permission").Select(c => c.Value).ToArray();

        var expClaim = FindFirst(user, JwtRegisteredClaimNames.Exp);
        var expiresAt = long.TryParse(expClaim, out var expSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime
            : DateTime.UtcNow.AddHours(1);

        return new UserContext
        {
            UserId = userId ?? string.Empty,
            UserName = email ?? string.Empty,
            TenantId = tenantId ?? string.Empty,
            Roles = roles,
            Permissions = permissions,
            ExpiresAt = expiresAt
        };
    }

    public bool HasPermission(string permission) => GetCurrentUser().HasPermission(permission);

    public bool HasRole(string role) => GetCurrentUser().HasRole(role);

    public string GetUserId() => GetCurrentUser().UserId;

    public string GetTenantId() => GetCurrentUser().TenantId;

    private static string? FindFirst(ClaimsPrincipal user, params string[] claimTypes) =>
        claimTypes.Select(user.FindFirst).FirstOrDefault(c => c != null)?.Value;
}
