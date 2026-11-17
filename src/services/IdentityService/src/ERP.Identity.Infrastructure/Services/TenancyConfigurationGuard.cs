using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Configuration;

namespace ERP.Identity.Infrastructure.Services;

/// <summary>
/// SEC-H2 (security review 2026-11): <c>Tenancy:FallbackTenantCode</c> makes EVERY host the resolver does not know
/// belong to that tenant (<see cref="TenantDomainService"/>). With SYSTEM that is the platform tenant: its sign-in form
/// and anonymous tenant sign-up on any IP or host name. It is a local-only convenience, so outside Development the
/// service refuses to start unless the operator opts in explicitly with
/// <c>Tenancy:AllowFallbackTenantOutsideDevelopment=true</c> (the local docker-compose stack does).
/// </summary>
public static class TenancyConfigurationGuard
{
    public const string FallbackKey = "Tenancy:FallbackTenantCode";
    public const string AllowKey = "Tenancy:AllowFallbackTenantOutsideDevelopment";

    public static void EnsureSafe(IConfiguration configuration, bool isDevelopment)
    {
        if (isDevelopment || string.IsNullOrWhiteSpace(configuration[FallbackKey]))
            return;

        if (bool.TryParse(configuration[AllowKey], out var allowed) && allowed)
            return;

        throw new ServiceMisconfiguredException(
            $"{FallbackKey} is set outside the Development environment: every unknown host would be served as that tenant. "
            + $"Remove it, or set {AllowKey}=true for a local single-machine stack only.");
    }
}
