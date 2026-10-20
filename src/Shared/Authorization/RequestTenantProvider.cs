using ERP.Shared.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Authorization;

/// <summary>
/// The tenant of the current HTTP request, for <see cref="ITenantProvider"/>:
/// <list type="number">
/// <item>an authenticated caller → the JWT's <c>tenant_id</c> (the Gateway already rejected
/// a token whose tenant ≠ the host's tenant);</item>
/// <item>an anonymous caller (public catalog, payment IPN, guest storefront) → the
/// <c>X-Tenant-Id</c> header the Gateway sets from the request host. The Gateway drops any
/// client-sent copy, and services are only reachable through it (Pha 1: no published
/// service ports), so the header can be trusted for anonymous reads.</item>
/// </list>
/// An authenticated caller never falls back to the header. An explicit
/// <see cref="TenantScope"/> (consumers, jobs) still wins over both.
/// </summary>
public static class RequestTenant
{
    public const string GatewayTenantIdHeader = "X-Tenant-Id";

    public static string? Resolve(HttpContext? httpContext)
    {
        if (httpContext is null)
            return null;

        if (httpContext.User?.Identity?.IsAuthenticated == true)
            return httpContext.User.FindFirst("tenant_id")?.Value;

        return httpContext.Request.Headers[GatewayTenantIdHeader].FirstOrDefault();
    }
}

public static class RequestTenantProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scoped <see cref="ITenantProvider"/> every tenant-scoped DbContext reads:
    /// <see cref="TenantScope"/> → JWT tenant → Gateway host tenant for anonymous requests.
    /// </summary>
    public static IServiceCollection AddRequestTenantProvider(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        return services.AddTenantProvider(sp =>
            RequestTenant.Resolve(sp.GetRequiredService<IHttpContextAccessor>().HttpContext));
    }
}
