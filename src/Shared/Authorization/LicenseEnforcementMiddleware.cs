using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Authorization;

/// <summary>
/// Identifies which module (see <see cref="ModuleCodes"/>) the hosting service
/// represents. Each service registers exactly one instance
/// (`builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Warehouse));`)
/// so <see cref="LicenseEnforcementMiddleware"/> knows what to check the caller's
/// tenant against.
/// </summary>
public class ModuleIdentity
{
    public string ModuleCode { get; }

    public ModuleIdentity(string moduleCode)
    {
        ModuleCode = moduleCode;
    }
}

/// <summary>
/// Rejects requests from a tenant whose current license (see TenantLicense in
/// IdentityService) does not include this service's module. Licensed module codes
/// are snapshotted into the JWT at login/refresh as repeated "licensed_module"
/// claims (same pattern as "permission") — this is an eventual-consistency model
/// bounded by the access-token lifetime, not a real-time per-request lookup, by
/// deliberate design choice.
///
/// Runs immediately after UserContextHeaderMiddleware (needs the populated
/// UserContext) and before UseAuthorization(). [AllowAnonymous] endpoints
/// (health checks, login, etc.) are exempt — same convention as [Authorize].
/// IdentityService does not run this middleware at all: it is the license issuer
/// and must stay reachable regardless of any tenant's license state.
/// </summary>
public class LicenseEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LicenseEnforcementMiddleware> _logger;

    public LicenseEnforcementMiddleware(RequestDelegate next, ILogger<LicenseEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, UserContext userContext, ModuleIdentity moduleIdentity)
    {
        var allowAnonymous = context.GetEndpoint()?.Metadata?.GetMetadata<IAllowAnonymous>() != null;

        if (!allowAnonymous && userContext.IsAuthenticated && !userContext.HasModule(moduleIdentity.ModuleCode))
        {
            _logger.LogWarning(
                "Tenant {TenantId} rejected: not licensed for module {ModuleCode}",
                userContext.TenantId, moduleIdentity.ModuleCode);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = $"Your organization is not licensed for this module ('{moduleIdentity.ModuleCode}')."
            });
            return;
        }

        await _next(context);
    }
}
