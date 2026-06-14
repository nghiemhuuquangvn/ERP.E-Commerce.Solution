namespace ERP.PO.Application.Authorization;

/// <summary>
/// Service để lấy thông tin user hiện tại từ HttpContext
/// </summary>
public interface IUserContextService
{
    /// <summary>
    /// Lấy UserContext từ HttpContext hiện tại
    /// </summary>
    UserContext GetCurrentUser();

    /// <summary>
    /// Kiểm tra user hiện tại có quyền cụ thể
    /// </summary>
    bool HasPermission(string permission);

    /// <summary>
    /// Kiểm tra user hiện tại có role cụ thể
    /// </summary>
    bool HasRole(string role);

    /// <summary>
    /// Kiểm tra user hiện tại có tất cả permissions
    /// </summary>
    bool HasAllPermissions(params string[] permissions);

    /// <summary>
    /// Kiểm tra user hiện tại có ít nhất một permission
    /// </summary>
    bool HasAnyPermission(params string[] permissions);

    /// <summary>
    /// Lấy ID của user hiện tại
    /// </summary>
    string GetUserId();

    /// <summary>
    /// Lấy ID của tenant mà user hiện tại thuộc về
    /// </summary>
    string GetTenantId();
}
