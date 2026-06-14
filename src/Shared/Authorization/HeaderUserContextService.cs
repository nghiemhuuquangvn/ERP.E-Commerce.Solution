using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Authorization;

/// <summary>
/// Implementation của IUserContextService
/// Lấy user info từ HTTP headers (được set bởi API Gateway)
/// </summary>
public class HeaderUserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<HeaderUserContextService> _logger;

    public HeaderUserContextService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<HeaderUserContextService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public UserContext GetCurrentUser()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            _logger.LogWarning("HttpContext is null");
            return new UserContext();
        }

        var userId = GetHeaderValue(context, "X-User-Id");
        var userName = GetHeaderValue(context, "X-User-Name");
        var tenantId = GetHeaderValue(context, "X-Tenant-Id");
        var tenantName = GetHeaderValue(context, "X-Tenant-Name");
        var rolesStr = GetHeaderValue(context, "X-User-Roles");
        var permissionsStr = GetHeaderValue(context, "X-User-Permissions");
        var expiresAtStr = GetHeaderValue(context, "X-User-ExpiresAt");

        var roles = ParseArray(rolesStr);
        var permissions = ParseArray(permissionsStr);

        var expiresAt = DateTime.UtcNow.AddHours(1); // Default 1 hour
        if (!string.IsNullOrEmpty(expiresAtStr) && long.TryParse(expiresAtStr, out var ticks))
        {
            expiresAt = new DateTime(ticks, DateTimeKind.Utc);
        }

        var userContext = new UserContext
        {
            UserId = userId,
            UserName = userName,
            TenantId = tenantId,
            TenantName = tenantName,
            Roles = roles,
            Permissions = permissions,
            ExpiresAt = expiresAt
        };

        _logger.LogInformation(
            "User {UserId} from tenant {TenantId} with roles {Roles}",
            userId, tenantId, string.Join(",", roles));

        return userContext;
    }

    public bool HasPermission(string permission)
    {
        return GetCurrentUser().HasPermission(permission);
    }

    public bool HasRole(string role)
    {
        return GetCurrentUser().HasRole(role);
    }

    public string GetUserId()
    {
        return GetCurrentUser().UserId;
    }

    public string GetTenantId()
    {
        return GetCurrentUser().TenantId;
    }

    private static string GetHeaderValue(HttpContext context, string headerName)
    {
        return context.Request.Headers.TryGetValue(headerName, out var value)
            ? value.ToString()
            : string.Empty;
    }

    private static string[] ParseArray(string value)
    {
        if (string.IsNullOrEmpty(value))
            return Array.Empty<string>();

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
    }
}
