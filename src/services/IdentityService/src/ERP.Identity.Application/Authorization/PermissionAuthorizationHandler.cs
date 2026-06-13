using ERP.Identity.Api.Authorization.Extensions;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Application.Interfaces.Mapping;
using ERP.Identity.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;

namespace ERP.Identity.Infrastructure.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserRoleResolver _userRoleResolver;
    private readonly IRolePermissionResolver _rolePermissionResolver;
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(IUserRoleResolver userRoleResolver, IRolePermissionResolver rolePermissionResolver, IPermissionService permissionService)
    {
        _userRoleResolver = userRoleResolver;
        _rolePermissionResolver = rolePermissionResolver;
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
            return;

        var userId = context.User.FindFirst("sub")?.Value;
        var tenantId = context.User.FindFirst("tenant_id")?.Value;

        if (userId == null || tenantId == null)
            return;

        if (context.Resource == null)
            return;

        var permissions = context.Resource.GetRequiredPermissions(); // từ attribute

        var hasPermission = await _permissionService.HasPermissionAsync(Guid.Parse(userId), Guid.Parse(tenantId), permissions);

        if (hasPermission)
            context.Succeed(requirement);
    }
}