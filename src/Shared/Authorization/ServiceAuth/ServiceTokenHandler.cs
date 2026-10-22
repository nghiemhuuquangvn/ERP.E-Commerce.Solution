using System.Net.Http.Headers;
using ERP.Shared.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ERP.Shared.Authorization.ServiceAuth;

/// <summary>
/// Adds this service's token (<see cref="ServiceTokenIssuer"/>) to every request of an
/// HttpClient that calls another service's <c>/internal</c> endpoints. The token carries the
/// current tenant (explicit <see cref="TenantScope"/>, else the caller's JWT / Gateway tenant)
/// and the user being served. No tenant → the request is not sent (fail closed), unless the
/// client is declared tenant-less (e.g. resolving a host to its tenant).
/// </summary>
public sealed class ServiceTokenHandler : DelegatingHandler
{
    private readonly ServiceTokenIssuer _issuer;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _targetService;
    private readonly bool _tenantless;

    public ServiceTokenHandler(ServiceTokenIssuer issuer, IHttpContextAccessor httpContextAccessor, string targetService, bool tenantless)
    {
        _issuer = issuer;
        _httpContextAccessor = httpContextAccessor;
        _targetService = targetService;
        _tenantless = tenantless;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        Guid? tenantId = null;
        if (!_tenantless)
        {
            tenantId = TenantScope.Current
                ?? (Guid.TryParse(RequestTenant.Resolve(httpContext), out var parsed) && parsed != Guid.Empty ? parsed : null);
            if (tenantId is null)
                throw new InvalidOperationException(
                    $"No tenant for the call to '{_targetService}': service-to-service calls are made for one tenant.");
        }

        var actor = httpContext?.User?.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            : null;

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.CreateToken(_targetService, tenantId, actor));
        return base.SendAsync(request, cancellationToken);
    }
}

public static class ServiceTokenHttpClientBuilderExtensions
{
    /// <summary>
    /// Signs every request of this client with this service's token for
    /// <paramref name="targetService"/> (its <c>ServiceAuth:Name</c>). Requires
    /// <c>ServiceAuth:Name</c> and <c>ServiceAuth:SigningKeys</c>; call
    /// <c>app.MapServiceJwks()</c> so the target can verify the tokens.
    /// </summary>
    public static IHttpClientBuilder AddServiceToken(this IHttpClientBuilder builder, string targetService, bool tenantless = false)
    {
        builder.Services.AddServiceTokenIssuer();
        builder.Services.AddHttpContextAccessor();
        return builder.AddHttpMessageHandler(sp => new ServiceTokenHandler(
            sp.GetRequiredService<ServiceTokenIssuer>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            targetService,
            tenantless));
    }

    public static IServiceCollection AddServiceTokenIssuer(this IServiceCollection services)
    {
        services.TryAddSingleton(sp => ServiceTokenIssuer.FromConfiguration(
            sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>()));
        return services;
    }
}
