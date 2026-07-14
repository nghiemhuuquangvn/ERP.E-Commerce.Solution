using ERP.Identity.Api.Authorization.Extensions;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Application.Interfaces.Mapping;
using ERP.Identity.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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

        // Dual-lookup for "sub": a registered JWT claim name gets silently renamed to
        // ClaimTypes.NameIdentifier if MapInboundClaims is ever true (the ASP.NET Core
        // default) — this handler gating every [RequirePermission] endpoint must keep
        // resolving the caller even if that config regresses. "tenant_id" is custom,
        // never touched by the inbound claim map, so a single lookup is safe.
        var userId = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = context.User.FindFirst("tenant_id")?.Value;

        if (userId == null || tenantId == null)
            return;

        if (context.Resource == null)
            return;

        // SuperAdmin bypasses granular permission checks entirely. Without this,
        // SuperAdmin only has whatever fine-grained codes happen to be seeded into
        // RolePermissions — every new [RequirePermission] code added later (e.g.
        // ViewTenants, ViewUsers vs ManageUsers) silently locks SuperAdmin out
        // until someone remembers to re-grant it, which is exactly what happened.
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        var permissions = context.Resource.GetRequiredPermissions(); // từ attribute

        var hasPermission = await _permissionService.HasPermissionAsync(Guid.Parse(userId), Guid.Parse(tenantId), permissions);

        if (hasPermission)
            context.Succeed(requirement);
    }
}