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
        var value = controller.User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(value, out var tenantId) ? tenantId : null;
    }

    public static Guid? GetCallerUserId(this ControllerBase controller)
    {
        var value = controller.User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
