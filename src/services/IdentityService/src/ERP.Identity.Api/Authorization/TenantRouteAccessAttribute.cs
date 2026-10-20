using ERP.Identity.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.Identity.Api.Authorization;

/// <summary>
/// Declarative cross-tenant guard. Put it on a controller (or action): whenever the request
/// carries a <c>tenantId</c> (route value or query string) that is not the caller's own
/// tenant, the caller must be in the platform tenant and hold
/// <see cref="PlatformPermission"/> — otherwise 403. Requests without a tenantId are not
/// affected, so it is safe at class level and automatically covers actions added later.
/// <code>[TenantRouteAccess(PermissionConstants.Settings_SystemManage)]</code>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TenantRouteAccessAttribute : Attribute, IAsyncActionFilter
{
    public TenantRouteAccessAttribute(string platformPermission)
    {
        PlatformPermission = platformPermission;
    }

    public string PlatformPermission { get; }

    /// <summary>Name of the route value / query parameter holding the target tenant.</summary>
    public string Key { get; init; } = "tenantId";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var raw = context.RouteData.Values.TryGetValue(Key, out var routeValue)
            ? routeValue?.ToString()
            : context.HttpContext.Request.Query[Key].ToString();

        if (!string.IsNullOrEmpty(raw))
        {
            if (!Guid.TryParse(raw, out var targetTenantId))
            {
                context.Result = new BadRequestObjectResult(new { message = $"Invalid {Key}." });
                return;
            }

            var policy = context.HttpContext.RequestServices.GetRequiredService<ITenantAccessPolicy>();
            if (!await policy.CanAccessTenantAsync(targetTenantId, PlatformPermission, context.HttpContext.RequestAborted))
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        await next();
    }
}
