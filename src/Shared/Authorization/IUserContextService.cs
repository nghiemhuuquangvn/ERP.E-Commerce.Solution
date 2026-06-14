namespace ERP.Shared.Authorization;

/// <summary>
/// Interface để lấy thông tin user hiện tại
/// Triển khai bởi HeaderUserContextService (lấy từ HTTP headers)
/// </summary>
public interface IUserContextService
{
    /// <summary>
    /// Lấy UserContext từ HTTP headers
    /// </summary>
    UserContext GetCurrentUser();

    /// <summary>
    /// Kiểm tra user có quyền cụ thể không
    /// </summary>
    bool HasPermission(string permission);

    /// <summary>
    /// Kiểm tra user có role cụ thể không
    /// </summary>
    bool HasRole(string role);

    /// <summary>
    /// Lấy user ID
    /// </summary>
    string GetUserId();

    /// <summary>
    /// Lấy tenant ID
    /// </summary>
    string GetTenantId();
}
