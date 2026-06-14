using System.Security.Claims;
using ERP.PO.Application.Authorization;

namespace ERP.PO.Api.Authorization;

/// <summary>
/// Triển khai IUserContextService - Lấy thông tin user từ HttpContext/JWT
/// </summary>
public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserContextService> _logger;

    public UserContextService(IHttpContextAccessor httpContextAccessor, ILogger<UserContextService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public UserContext GetCurrentUser()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User == null)
        {
            _logger.LogWarning("No user context available");
            return new UserContext { UserId = "anonymous", TenantId = "default" };
        }

        var user = context.User;
        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var userName = user.FindFirst("preferred_username")?.Value ?? user.FindFirst(ClaimTypes.Name)?.Value ?? "unknown";
        var tenantId = user.FindFirst("tenant_id")?.Value ?? "default";
        var tenantName = user.FindFirst("tenant_name")?.Value ?? "Default Tenant";

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var permissions = ExtractPermissionsFromRoles(roles);

        var expiresAtStr = user.FindFirst("exp")?.Value;
        var expiresAt = DateTime.UtcNow.AddHours(1);
        if (long.TryParse(expiresAtStr, out var expUnix))
        {
            expiresAt = UnixTimeStampToDateTime(expUnix);
        }

        return new UserContext
        {
            UserId = userId,
            UserName = userName,
            TenantId = tenantId,
            TenantName = tenantName,
            Roles = roles,
            Permissions = permissions,
            ExpiresAt = expiresAt
        };
    }

    public bool HasPermission(string permission)
    {
        return GetCurrentUser().HasPermission(permission);
    }

    public bool HasRole(string role)
    {
        return GetCurrentUser().HasRole(role);
    }

    public bool HasAllPermissions(params string[] permissions)
    {
        return GetCurrentUser().HasAllPermissions(permissions);
    }

    public bool HasAnyPermission(params string[] permissions)
    {
        return GetCurrentUser().HasAnyPermission(permissions);
    }

    public string GetUserId()
    {
        return GetCurrentUser().UserId;
    }

    public string GetTenantId()
    {
        return GetCurrentUser().TenantId;
    }

    /// <summary>
    /// Tính toán permissions từ roles
    /// </summary>
    private string[] ExtractPermissionsFromRoles(string[] roles)
    {
        var permissions = new HashSet<string>();

        foreach (var role in roles)
        {
            var rolePermissions = RoleDefinitions.GetRolePermissions(role);
            foreach (var permission in rolePermissions)
            {
                permissions.Add(permission);
            }
        }

        return permissions.ToArray();
    }

    /// <summary>
    /// Convert Unix timestamp to DateTime
    /// </summary>
    private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
        return dateTime;
    }
}
