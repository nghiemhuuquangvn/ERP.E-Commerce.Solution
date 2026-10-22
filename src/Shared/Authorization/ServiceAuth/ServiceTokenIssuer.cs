using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ERP.Shared.Security.Certificates;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Shared.Authorization.ServiceAuth;

public static class ServiceAuthDefaults
{
    /// <summary>Authentication scheme for service-to-service tokens (receivers).</summary>
    public const string Scheme = "ErpService";

    /// <summary>Issuer / audience prefix: <c>erp-svc:&lt;service name&gt;</c>.</summary>
    public const string IssuerPrefix = "erp-svc:";

    /// <summary>Where every calling service publishes its public keys (internal network only).</summary>
    public const string JwksPath = "/internal/jwks.json";

    public const string TenantClaim = "tenant_id";

    /// <summary>The user on whose behalf the calling service acts, if any.</summary>
    public const string ActorClaim = "act_sub";

    public static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(60);

    public static string IssuerFor(string serviceName) => IssuerPrefix + serviceName;
}

/// <summary>
/// Signs this service's short-lived tokens for calls to another service's <c>/internal</c>
/// endpoints. The private key belongs to this service alone (<c>ServiceAuth:SigningKeys</c> —
/// Key Vault in production, Ephemeral/File locally); receivers validate with the public keys
/// published at <see cref="ServiceAuthDefaults.JwksPath"/>. A leaked key lets an attacker act
/// as this one service — never as a user or another service.
/// </summary>
public sealed class ServiceTokenIssuer
{
    private static readonly TimeSpan RenewBeforeExpiry = TimeSpan.FromSeconds(15);

    private readonly CertificateSet _certificates;
    private readonly SigningCredentials _credentials;
    private readonly TimeProvider _time;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public ServiceTokenIssuer(string serviceName, CertificateSet certificates, TimeProvider? time = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new InvalidOperationException("Configuration value 'ServiceAuth:Name' is missing.");
        ServiceName = serviceName;
        _certificates = certificates;
        _credentials = new X509SigningCredentials(certificates.Current, SecurityAlgorithms.RsaSha256);
        _time = time ?? TimeProvider.System;
        JwksJson = BuildJwks(certificates.All);
    }

    public string ServiceName { get; }

    /// <summary>Public keys of every loaded certificate (current + previous), as a JWKS document.</summary>
    public string JwksJson { get; }

    /// <summary>
    /// A token for <paramref name="targetService"/>, re-used until shortly before it expires.
    /// <paramref name="tenantId"/> is the tenant the call is made for (null only for
    /// tenant-less lookups); <paramref name="actorUserId"/> the user on whose behalf, if any.
    /// </summary>
    public string CreateToken(string targetService, Guid? tenantId, string? actorUserId)
    {
        var key = (targetService, tenantId, actorUserId);
        if (_cache.TryGetValue(key, out string? cached) && cached is not null)
            return cached;

        var now = _time.GetUtcNow();
        var expires = now.Add(ServiceAuthDefaults.TokenLifetime);
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = ServiceName,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
        };
        if (tenantId is { } tenant && tenant != Guid.Empty)
            claims[ServiceAuthDefaults.TenantClaim] = tenant.ToString();
        if (!string.IsNullOrEmpty(actorUserId))
            claims[ServiceAuthDefaults.ActorClaim] = actorUserId;

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ServiceAuthDefaults.IssuerFor(ServiceName),
            Audience = ServiceAuthDefaults.IssuerFor(targetService),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = _credentials
        });

        _cache.Set(key, token, ServiceAuthDefaults.TokenLifetime - RenewBeforeExpiry);
        return token;
    }

    /// <summary>
    /// <c>ServiceAuth:Name</c> + certificates from <c>ServiceAuth:SigningKeys</c>
    /// (<c>KeyVault</c> | <c>File</c> | <c>Ephemeral</c>). Missing configuration stops start-up.
    /// </summary>
    public static ServiceTokenIssuer FromConfiguration(IConfiguration configuration, IHostEnvironment environment) =>
        new(configuration["ServiceAuth:Name"] ?? string.Empty,
            CertificateLoader.Load(configuration, "ServiceAuth:SigningKeys", environment.ContentRootPath,
                environment.IsDevelopment(), CertificatePurpose.Signing, allowEphemeral: true));

    private static string BuildJwks(IEnumerable<X509Certificate2> certificates)
    {
        var keys = certificates.Select(certificate =>
        {
            using var rsa = certificate.GetRSAPublicKey()!;
            var parameters = rsa.ExportParameters(includePrivateParameters: false);
            return new Dictionary<string, string>
            {
                ["kty"] = "RSA",
                ["use"] = "sig",
                ["alg"] = SecurityAlgorithms.RsaSha256,
                // Same kid as X509SigningCredentials puts in the token header.
                ["kid"] = new X509SecurityKey(certificate).KeyId,
                ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
                ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!)
            };
        });
        return JsonSerializer.Serialize(new { keys });
    }
}

/// <summary>Claims of an authenticated service-to-service call.</summary>
public static class ServiceCallerPrincipal
{
    /// <summary>The calling service's name, from the validated issuer (not from <c>sub</c>).</summary>
    public static string? CallerService(this ClaimsPrincipal principal)
    {
        var issuer = principal.FindFirst(JwtRegisteredClaimNames.Iss)?.Value;
        return issuer is not null && issuer.StartsWith(ServiceAuthDefaults.IssuerPrefix, StringComparison.Ordinal)
            ? issuer[ServiceAuthDefaults.IssuerPrefix.Length..]
            : null;
    }
}
