using ERP.PO.Application.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.PO.Api.Authorization;

/// <summary>
/// Custom Authorization Attribute để kiểm tra permissions
/// Sử dụng: [AuthorizePermission(PermissionConstants.PurchaseOrder_Create)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredPermissions;

    /// <summary>
    /// Khởi tạo với một hoặc nhiều permissions yêu cầu
    /// </summary>
    /// <param name="permissions">Danh sách permissions cần thiết (phải có TẤT CẢ)</param>
    public AuthorizePermissionAttribute(params string[] permissions)
    {
        _requiredPermissions = permissions ?? Array.Empty<string>();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Lấy IUserContextService từ DI Container
        var userContextService = context.HttpContext.RequestServices.GetService(typeof(IUserContextService)) as IUserContextService;

        if (userContextService == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Lấy thông tin user hiện tại
        var user = userContextService.GetCurrentUser();

        // Nếu không có user (anonymous)
        if (string.IsNullOrEmpty(user.UserId) || user.UserId == "anonymous")
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Kiểm tra user có tất cả required permissions
        if (!user.HasAllPermissions(_requiredPermissions))
        {
            context.Result = new ForbidResult();
            return;
        }

        // User có quyền, cho phép tiếp tục
    }
}

/// <summary>
/// Custom Authorization Attribute cho ANY permission (ít nhất một)
/// Sử dụng: [AuthorizeAnyPermission(Permission1, Permission2)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAnyPermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredPermissions;

    public AuthorizeAnyPermissionAttribute(params string[] permissions)
    {
        _requiredPermissions = permissions ?? Array.Empty<string>();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var userContextService = context.HttpContext.RequestServices.GetService(typeof(IUserContextService)) as IUserContextService;

        if (userContextService == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = userContextService.GetCurrentUser();

        if (string.IsNullOrEmpty(user.UserId) || user.UserId == "anonymous")
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!user.HasAnyPermission(_requiredPermissions))
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}

/// <summary>
/// Custom Authorization Attribute cho Role-based (phải có ít nhất một role)
/// Sử dụng: [AuthorizeRole("Admin", "Manager")]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredRoles;

    public AuthorizeRoleAttribute(params string[] roles)
    {
        _requiredRoles = roles ?? Array.Empty<string>();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var userContextService = context.HttpContext.RequestServices.GetService(typeof(IUserContextService)) as IUserContextService;

        if (userContextService == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = userContextService.GetCurrentUser();

        if (string.IsNullOrEmpty(user.UserId) || user.UserId == "anonymous")
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!user.HasAnyRole(_requiredRoles))
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}
