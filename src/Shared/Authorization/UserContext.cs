namespace ERP.Shared.Authorization;

/// <summary>
/// Thông tin user hiện tại
/// Được tạo từ JWT claims hoặc HTTP headers
/// </summary>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public DateTime ExpiresAt { get; set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(TenantId);

    public bool IsTokenExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Kiểm tra user có quyền cụ thể không
    /// </summary>
    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kiểm tra user có TẤT CẢ quyền không
    /// </summary>
    public bool HasAllPermissions(params string[] permissions)
    {
        return permissions.All(HasPermission);
    }

    /// <summary>
    /// Kiểm tra user có ÍT NHẤT một quyền không
    /// </summary>
    public bool HasAnyPermission(params string[] permissions)
    {
        return permissions.Any(HasPermission);
    }

    /// <summary>
    /// Kiểm tra user có role cụ thể không
    /// </summary>
    public bool HasRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kiểm tra user có TẤT CẢ roles không
    /// </summary>
    public bool HasAllRoles(params string[] roles)
    {
        return roles.All(HasRole);
    }

    /// <summary>
    /// Kiểm tra user có ÍT NHẤT một role không
    /// </summary>
    public bool HasAnyRole(params string[] roles)
    {
        return roles.Any(HasRole);
    }
}
