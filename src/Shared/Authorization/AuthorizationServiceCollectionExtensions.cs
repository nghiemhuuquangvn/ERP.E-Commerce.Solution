using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Secure-by-default authorization for a service: the FallbackPolicy requires an
    /// authenticated caller for every endpoint that declares nothing, so an endpoint that
    /// forgets its attribute is never anonymous. Public endpoints must say so explicitly
    /// ([AllowAnonymous] on actions, .AllowAnonymous() on MapHealthChecks/MapHub).
    /// Permission checks themselves stay declarative: [AuthorizePermission(...)].
    /// </summary>
    public static IServiceCollection AddErpAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        return services;
    }
}

/// <summary>
/// Declares that an action is deliberately open to ANY signed-in user (e.g. a customer
/// posting a product review) — no permission required. Use it instead of a bare
/// [Authorize] so reviewers and the architecture test can tell "intentionally any user"
/// apart from "forgot the permission".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeAnyAuthenticatedUserAttribute : AuthorizeAttribute
{
}
