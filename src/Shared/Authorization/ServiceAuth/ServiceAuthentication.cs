using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Shared.Authorization.ServiceAuth;

public static class ServiceAuthenticationExtensions
{
    /// <summary>
    /// Receiving side of service-to-service calls: the <see cref="ServiceAuthDefaults.Scheme"/>
    /// scheme accepts only tokens of the callers listed in <c>ServiceAuth:TrustedCallers</c>
    /// (name → that service's JWKS URL), addressed to this service (<c>ServiceAuth:Name</c>),
    /// RS256, at most 60 s old. Each caller's keys only validate that caller's tokens. User
    /// tokens are refused here, and service tokens are refused by the user "Bearer" scheme.
    /// Endpoints opt in with <see cref="AuthorizeInternalServiceAttribute"/>.
    /// </summary>
    public static IServiceCollection AddErpServiceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var parameters = CreateValidationParameters(configuration, out var callers);
        var requireHttps = configuration.GetValue("Jwt:RequireHttpsMetadata", true);

        services.AddAuthentication()
            .AddJwtBearer(ServiceAuthDefaults.Scheme, options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = requireHttps;
                options.RefreshOnIssuerKeyNotFound = true;
                options.TokenValidationParameters = parameters;
                options.ConfigurationManager = new TrustedCallerKeys(callers, requireHttps);
            });
        return services;
    }

    /// <summary>Validation rules without the callers' keys (added from their JWKS). Public for tests.</summary>
    public static TokenValidationParameters CreateValidationParameters(IConfiguration configuration, out IReadOnlyDictionary<string, string> trustedCallers)
    {
        var name = configuration["ServiceAuth:Name"]
            ?? throw new InvalidOperationException("Configuration value 'ServiceAuth:Name' is missing.");
        var callers = configuration.GetSection("ServiceAuth:TrustedCallers").GetChildren()
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .ToDictionary(c => c.Key, c => c.Value!, StringComparer.Ordinal);
        if (callers.Count == 0)
            throw new InvalidOperationException("Configuration section 'ServiceAuth:TrustedCallers' is empty — no service may call this one.");
        trustedCallers = callers;

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = callers.Keys.Select(ServiceAuthDefaults.IssuerFor).ToList(),
            ValidateAudience = true,
            ValidAudience = ServiceAuthDefaults.IssuerFor(name),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            // Exact issuer list only (the handler would also trust the fetched configuration's issuer).
            IssuerValidator = (issuer, _, p) => p.ValidIssuers.Contains(issuer, StringComparer.Ordinal)
                ? issuer
                : throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' is not a trusted caller."),
            // A key published by caller A must never validate a token claiming to come from B.
            IssuerSigningKeyValidator = (key, token, _) => TrustedCallerKeys.OwnerOf(key) is { } owner
                && string.Equals(ServiceAuthDefaults.IssuerFor(owner), token.Issuer, StringComparison.Ordinal)
        };
    }

    /// <summary>
    /// Publishes this service's public token keys at <see cref="ServiceAuthDefaults.JwksPath"/>
    /// (anonymous; internal network — nginx and the Gateway don't route <c>/internal</c>).
    /// Also loads the signing certificates now, so a misconfiguration stops start-up.
    /// </summary>
    public static IEndpointConventionBuilder MapServiceJwks(this IEndpointRouteBuilder endpoints)
    {
        var issuer = endpoints.ServiceProvider.GetRequiredService<ServiceTokenIssuer>();
        return endpoints.MapGet(ServiceAuthDefaults.JwksPath, () => Results.Content(issuer.JwksJson, "application/json"))
            .AllowAnonymous();
    }
}

/// <summary>
/// The keys of every trusted caller, fetched from each caller's JWKS (cached, refreshed on an
/// unknown kid). One unreachable caller doesn't stop the others' tokens from validating.
/// </summary>
public sealed class TrustedCallerKeys : IConfigurationManager<OpenIdConnectConfiguration>
{
    private static readonly ConditionalWeakTable<SecurityKey, string> Owners = new();

    private readonly IReadOnlyDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _callers;
    private readonly ILogger _logger;

    public TrustedCallerKeys(IReadOnlyDictionary<string, string> callers, bool requireHttps, ILogger? logger = null)
        : this(callers, new HttpDocumentRetriever { RequireHttps = requireHttps }, logger)
    {
    }

    /// <param name="callers">Caller name → its JWKS URL.</param>
    /// <param name="documents">Fetches the JWKS documents (HTTP in production; a fake in tests).</param>
    public TrustedCallerKeys(IReadOnlyDictionary<string, string> callers, IDocumentRetriever documents, ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _callers = callers.ToDictionary(
            c => c.Key,
            c => new ConfigurationManager<OpenIdConnectConfiguration>(c.Value, new JwksConfigurationRetriever(), documents)
            {
                RefreshInterval = TimeSpan.FromSeconds(30)
            });
    }

    internal static string? OwnerOf(SecurityKey key) => Owners.TryGetValue(key, out var owner) ? owner : null;

    internal static void SetOwner(SecurityKey key, string caller) => Owners.AddOrUpdate(key, caller);

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        var merged = new OpenIdConnectConfiguration();
        foreach (var (caller, manager) in _callers)
        {
            try
            {
                var configuration = await manager.GetConfigurationAsync(cancel);
                foreach (var key in configuration.SigningKeys)
                {
                    SetOwner(key, caller);
                    merged.SigningKeys.Add(key);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not fetch the token keys of trusted caller {Caller}", caller);
            }
        }
        return merged;
    }

    public void RequestRefresh()
    {
        foreach (var manager in _callers.Values)
            manager.RequestRefresh();
    }
}

/// <summary>
/// Marks an endpoint as internal: callable only by the listed services, with a
/// service-to-service token (never a user token, never anonymously). Use it on
/// <c>internal/...</c> routes only — the architecture tests enforce both directions.
/// The call's tenant (token claim <c>tenant_id</c>) becomes the request tenant, so tenant
/// filters apply as usual; <see cref="RequiresTenant"/> = false only for tenant-less lookups.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeInternalServiceAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    public AuthorizeInternalServiceAttribute(params string[] callers)
    {
        if (callers.Length == 0)
            throw new ArgumentException("List the services allowed to call this endpoint.", nameof(callers));
        Callers = callers;
        AuthenticationSchemes = ServiceAuthDefaults.Scheme;
    }

    public IReadOnlyList<string> Callers { get; }

    public bool RequiresTenant { get; set; } = true;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var caller = user.CallerService();
        if (caller is null || !Callers.Contains(caller, StringComparer.Ordinal))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        if (RequiresTenant && !(Guid.TryParse(user.FindFirst(ServiceAuthDefaults.TenantClaim)?.Value, out var tenant) && tenant != Guid.Empty))
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }
}
