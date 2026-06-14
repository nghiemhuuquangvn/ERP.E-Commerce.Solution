namespace ERP.PO.Application.Authorization;

/// <summary>
/// Thông tin context của User hiện tại (từ JWT)
/// </summary>
public class UserContext
{
    /// <summary>
    /// ID của user
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Tên/email của user
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// ID của tenant/organization mà user thuộc về
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Tên của tenant/organization
    /// </summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>
    /// Danh sách roles của user
    /// </summary>
    public string[] Roles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Danh sách permissions của user (tính toán từ roles)
    /// </summary>
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Thời gian JWT hết hạn
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Kiểm tra user có quyền cụ thể không
    /// </summary>
    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission);
    }

    /// <summary>
    /// Kiểm tra user có role cụ thể không
    /// </summary>
    public bool HasRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kiểm tra user có tất cả permissions cụ thể không
    /// </summary>
    public bool HasAllPermissions(params string[] permissions)
    {
        return permissions.All(HasPermission);
    }

    /// <summary>
    /// Kiểm tra user có ít nhất một permission cụ thể
    /// </summary>
    public bool HasAnyPermission(params string[] permissions)
    {
        return permissions.Any(HasPermission);
    }

    /// <summary>
    /// Kiểm tra user có ít nhất một role cụ thể
    /// </summary>
    public bool HasAnyRole(params string[] roles)
    {
        return roles.Any(HasRole);
    }
}
