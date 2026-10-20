using ERP.Shared.Authorization.TokenRevocation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ERP.Shared.Authorization;

/// <summary>
/// The one JWT validation setup shared by every service and the Gateway.
///
/// Access tokens are signed by IdentityService with its RS256 certificate (private key only
/// in Identity — Key Vault in production). Everyone else validates with Identity's public
/// keys, fetched from its JWKS endpoint (<c>Jwt:Authority</c> + <c>/.well-known/jwks.json</c>,
/// internal network) and re-fetched when a token carries an unknown <c>kid</c> (rotation).
///
/// Transition (release 1 of the RS256 cutover): while <c>Jwt:Key</c> is still configured,
/// HS256 tokens signed with that shared key are accepted too. Release 2 removes the key —
/// see docs/business/Identity/SigningKeys.md.
///
/// Only Identity's login tokens (issuer <c>Jwt:Issuer</c>) are accepted — not OpenIddict's
/// <c>urn:</c> issuer, whose client_credentials permissions don't go through
/// EffectivePermissions yet.
/// </summary>
public static class ErpJwtBearerOptions
{
    public const string DefaultAuthority = "http://identityservice:5001";
    public const string JwksPath = "/.well-known/jwks.json";

    /// <summary>
    /// Configures signature (RS256 via JWKS, HS256 only during the transition), issuer,
    /// audience and lifetime validation, plus the shared token revocation check.
    /// Callers may add further events (e.g. hub ticket handling) afterwards.
    /// </summary>
    public static void Configure(JwtBearerOptions options, IConfiguration configuration)
    {
        // Claim types stay exactly as TokenService writes them ("sub", "email", ...) instead of
        // the legacy ClaimTypes.* URIs — HttpContext.User.FindFirst("sub") relies on this.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = CreateValidationParameters(configuration);

        var requireHttps = configuration.GetValue("Jwt:RequireHttpsMetadata", true);
        options.RequireHttpsMetadata = requireHttps;
        options.RefreshOnIssuerKeyNotFound = true;
        options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            ResolveJwksUri(configuration),
            new JwksConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = requireHttps })
        {
            // Identity may start after this service: retry a failed key fetch quickly
            // instead of answering 401 for minutes.
            RefreshInterval = TimeSpan.FromSeconds(30)
        };

        // A signature-valid token can still be revoked: logout (jti), or issued before its
        // user's / tenant's last security-relevant change (iat). Fails open if the store is down.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (context.Principal is null)
                    return;
                var validator = context.HttpContext.RequestServices.GetRequiredService<TokenRevocationValidator>();
                if (await validator.IsRevokedAsync(context.Principal, context.HttpContext.RequestAborted))
                    context.Fail("Token has been revoked.");
            }
        };
    }

    /// <summary>
    /// Validation rules without the signing keys fetched from JWKS (the handler adds those).
    /// Public for tests.
    /// </summary>
    public static TokenValidationParameters CreateValidationParameters(IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Configuration value 'Jwt:Issuer' is missing.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Configuration value 'Jwt:Audience' is missing.");

        var algorithms = new List<string> { SecurityAlgorithms.RsaSha256 };
        var signingKeys = new List<SecurityKey>();

        // Transition only: the shared symmetric key. Release 2 removes Jwt:Key from every
        // service except Identity.
        var legacyKey = configuration["Jwt:Key"];
        if (!string.IsNullOrEmpty(legacyKey))
        {
            JwtKeyGuard.EnsureUsable(legacyKey, JwtKeyGuard.IsDevelopmentFromEnvironmentVariables());
            algorithms.Add(SecurityAlgorithms.HmacSha256);
            signingKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(legacyKey)));
        }

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = algorithms,
            IssuerSigningKeys = signingKeys,
            ValidAudience = audience,
            ValidIssuer = issuer,
            // Exact match only: the JWT handler would otherwise also trust the issuer of the
            // fetched configuration (OpenIddict's urn: issuer).
            IssuerValidator = (tokenIssuer, _, _) => string.Equals(tokenIssuer, issuer, StringComparison.Ordinal)
                ? tokenIssuer
                : throw new SecurityTokenInvalidIssuerException($"Issuer '{tokenIssuer}' is not accepted.")
        };
    }

    public static string ResolveJwksUri(IConfiguration configuration)
    {
        var explicitUri = configuration["Jwt:JwksUri"];
        if (!string.IsNullOrWhiteSpace(explicitUri))
            return explicitUri;
        var authority = configuration["Jwt:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
            authority = DefaultAuthority;
        return authority.TrimEnd('/') + JwksPath;
    }
}

/// <summary>Reads a bare JWKS document (no OpenID discovery — its issuer must not be trusted).</summary>
public sealed class JwksConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
    {
        var document = await retriever.GetDocumentAsync(address, cancel);
        var keySet = new JsonWebKeySet(document);
        var configuration = new OpenIdConnectConfiguration { JwksUri = address, JsonWebKeySet = keySet };
        foreach (var key in keySet.GetSigningKeys())
            configuration.SigningKeys.Add(key);
        return configuration;
    }
}
