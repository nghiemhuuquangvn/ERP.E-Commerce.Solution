using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Identity.Api.Authorization;

/// <summary>
/// Replaces the duplicated `User.FindFirst("tenant_id")?.Value` / TryParse blocks that
/// were scattered across UsersController/RolesController — every tenant-boundary check
/// in this service should read the caller's own JWT claims through here, not re-parse
/// claims ad hoc.
/// </summary>
public static class ControllerTenantExtensions
{
    public static Guid? GetCallerTenantId(this ControllerBase controller)
    {
        // Custom claim type, never touched by ASP.NET Core's inbound claim map — safe
        // to read as-is regardless of MapInboundClaims.
        var value = controller.User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(value, out var tenantId) ? tenantId : null;
    }

    public static Guid? GetCallerUserId(this ControllerBase controller)
    {
        // Dual-lookup: "sub" is a registered JWT claim name, so it gets silently
        // renamed to ClaimTypes.NameIdentifier if MapInboundClaims is ever true (the
        // ASP.NET Core default) — this must keep working even if that config
        // regresses, since this is the tenant/self-ownership boundary check.
        var value = controller.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? controller.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
