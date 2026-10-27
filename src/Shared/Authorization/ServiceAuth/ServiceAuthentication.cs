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
    /// RS256, at most 60 s old (none listed → every service token is refused). Each caller's
    /// keys only validate that caller's tokens. User
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
                // An unknown key id is looked up again before the token is judged (see TrustedCallerKeys); the manager is read from the options so it can be replaced.
                parameters.IssuerSigningKeyResolver = (_, _, kid, _) =>
                    (options.ConfigurationManager as TrustedCallerKeys)?.ResolveSigningKeys(kid) ?? [];
            });
        return services;
    }

    /// <summary>Validation rules without the callers' keys (added from their JWKS). Public for tests.</summary>
    public static TokenValidationParameters CreateValidationParameters(IConfiguration configuration, out IReadOnlyDictionary<string, string> trustedCallers)
    {
        var callers = configuration.GetSection("ServiceAuth:TrustedCallers").GetChildren()
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .ToDictionary(c => c.Key, c => c.Value!, StringComparer.Ordinal);
        // No trusted callers (e.g. a standalone deployment): every service token is refused,
        // so the /internal endpoints are simply unreachable.
        var name = configuration["ServiceAuth:Name"];
        if (string.IsNullOrWhiteSpace(name))
            name = callers.Count == 0
                ? "unnamed"
                : throw new InvalidOperationException("Configuration value 'ServiceAuth:Name' is missing.");
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
/// The keys of every trusted caller, fetched from each caller's JWKS and cached. One unreachable caller doesn't stop the others' tokens from validating.
/// A token signed with a key that is not cached (the caller restarted: service tokens use an ephemeral key) is not refused: the JWKS is fetched again
/// right then, at most once per <see cref="MinimumRefreshInterval"/> per caller, and the token is validated against the new keys (R38 - the library's own
/// refresh runs in the background, so the first call after a redeploy used to get 401).
/// </summary>
public sealed class TrustedCallerKeys : IConfigurationManager<OpenIdConnectConfiguration>
{
    private static readonly ConditionalWeakTable<SecurityKey, string> Owners = new();

    /// <summary>The least time between two fetches of one caller's keys: bounds the traffic an attacker can cause with tokens of unknown keys.</summary>
    public static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromSeconds(2);

    /// <summary>Keys are fetched again after this even when nothing asked for it.</summary>
    private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(1);

    private sealed class CallerState(string url)
    {
        public string Url { get; } = url;
        public IReadOnlyList<SecurityKey> Keys { get; set; } = [];
        public DateTimeOffset? FetchedAt { get; set; }
    }

    private readonly Dictionary<string, CallerState> _callers;
    private readonly IDocumentRetriever _documents;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _fetching = new(1, 1);

    public TrustedCallerKeys(IReadOnlyDictionary<string, string> callers, bool requireHttps, ILogger? logger = null)
        : this(callers, new HttpDocumentRetriever { RequireHttps = requireHttps }, logger)
    {
    }

    /// <param name="callers">Caller name → its JWKS URL.</param>
    /// <param name="documents">Fetches the JWKS documents (HTTP in production; a fake in tests).</param>
    public TrustedCallerKeys(IReadOnlyDictionary<string, string> callers, IDocumentRetriever documents, ILogger? logger = null, TimeProvider? time = null)
    {
        _documents = documents;
        _logger = logger ?? NullLogger.Instance;
        _time = time ?? TimeProvider.System;
        _callers = callers.ToDictionary(c => c.Key, c => new CallerState(c.Value));
    }

    internal static string? OwnerOf(SecurityKey key) => Owners.TryGetValue(key, out var owner) ? owner : null;

    internal static void SetOwner(SecurityKey key, string caller) => Owners.AddOrUpdate(key, caller);

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        await RefreshAsync(force: false, cancel);
        var merged = new OpenIdConnectConfiguration();
        lock (_gate)
            foreach (var state in _callers.Values)
                foreach (var key in state.Keys)
                    merged.SigningKeys.Add(key);
        return merged;
    }

    /// <summary>
    /// The cached keys that match <paramref name="kid"/> (all of them when the token names none). When none matches, the callers' keys are fetched again
    /// first (throttled) so a token signed after a restart is validated on its first call.
    /// </summary>
    public IReadOnlyList<SecurityKey> ResolveSigningKeys(string? kid)
    {
        RefreshAsync(force: false, CancellationToken.None).GetAwaiter().GetResult();
        var found = Matching(kid);
        if (found.Count > 0) return found;

        RefreshAsync(force: true, CancellationToken.None).GetAwaiter().GetResult();
        return Matching(kid);
    }

    private List<SecurityKey> Matching(string? kid)
    {
        lock (_gate)
            return _callers.Values.SelectMany(s => s.Keys)
                .Where(k => string.IsNullOrEmpty(kid) || string.Equals(k.KeyId, kid, StringComparison.Ordinal))
                .ToList();
    }

    /// <summary>The library asks for a refresh when it meets an unknown key; <see cref="ResolveSigningKeys"/> already did it before the token was judged.</summary>
    public void RequestRefresh()
    {
    }

    private async Task RefreshAsync(bool force, CancellationToken cancel)
    {
        await _fetching.WaitAsync(cancel);
        try
        {
            foreach (var (caller, state) in _callers)
            {
                var now = _time.GetUtcNow();
                var age = state.FetchedAt is { } at ? now - at : (TimeSpan?)null;
                var due = age is null || age >= MaximumAge || (force && age >= MinimumRefreshInterval);
                if (!due) continue;

                try
                {
                    var configuration = await new JwksConfigurationRetriever().GetConfigurationAsync(state.Url, _documents, cancel);
                    var keys = configuration.SigningKeys.ToList();
                    foreach (var key in keys) SetOwner(key, caller);
                    lock (_gate) state.Keys = keys;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not fetch the token keys of trusted caller {Caller}", caller);
                }
                // A caller that cannot be reached is tried again after the minimum interval, not on every call.
                lock (_gate) state.FetchedAt = now;
            }
        }
        finally
        {
            _fetching.Release();
        }
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
