using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.Shared.Authorization;

/// <summary>
/// Custom authorization attribute - kiểm tra user có TẤT CẢ permissions
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _requiredPermissions;

    public AuthorizePermissionAttribute(params string[] permissions)
    {
        _requiredPermissions = permissions;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userContextService = context.HttpContext.RequestServices
            .GetService(typeof(IUserContextService)) as IUserContextService;

        if (userContextService == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = userContextService.GetCurrentUser();

        // Check user authenticated
        if (!user.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check token not expired
        if (user.IsTokenExpired)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // SuperAdmin role bypasses all permission checks
        if (!user.HasRole("SuperAdmin") && !user.HasAllPermissions(_requiredPermissions))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// Custom authorization attribute - kiểm tra user có ÍT NHẤT một permission
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAnyPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _requiredPermissions;

    public AuthorizeAnyPermissionAttribute(params string[] permissions)
    {
        _requiredPermissions = permissions;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userContextService = context.HttpContext.RequestServices
            .GetService(typeof(IUserContextService)) as IUserContextService;

        if (userContextService == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = userContextService.GetCurrentUser();

        // Check user authenticated
        if (!user.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check token not expired
        if (user.IsTokenExpired)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // system:admin bypasses all permission checks
        if (!user.HasPermission("system:admin") && !user.HasAnyPermission(_requiredPermissions))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// Custom authorization attribute - kiểm tra user có TẤT CẢ roles
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeRoleAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _requiredRoles;

    public AuthorizeRoleAttribute(params string[] roles)
    {
        _requiredRoles = roles;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userContextService = context.HttpContext.RequestServices
            .GetService(typeof(IUserContextService)) as IUserContextService;

        if (userContextService == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = userContextService.GetCurrentUser();

        // Check user authenticated
        if (!user.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check token not expired
        if (user.IsTokenExpired)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // SuperAdmin role bypasses all role checks
        if (!user.HasRole("SuperAdmin") && !user.HasAllRoles(_requiredRoles))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await Task.CompletedTask;
    }
}
