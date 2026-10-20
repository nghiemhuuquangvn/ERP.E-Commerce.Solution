using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ERP.Shared.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ERP.Shared.UnitTests.Authorization;

/// <summary>
/// RS256/JWKS cutover: services accept Identity's RS256 tokens (public key from JWKS), HS256
/// only while the shared Jwt:Key is still configured, and only the login token issuer.
/// </summary>
public class ErpJwtBearerOptionsTests
{
    private const string Issuer = "ERP.Identity";
    private const string Audience = "ERP.Services";
    private const string LegacyKey = "a-fresh-random-test-key-of-at-least-32-bytes!";

    private static readonly RsaSecurityKey IdentityKey = NewRsaKey("identity-current");
    private static readonly RsaSecurityKey ForeignKey = NewRsaKey("someone-else");

    private static RsaSecurityKey NewRsaKey(string kid) => new(RSA.Create(2048)) { KeyId = kid };

    private static IConfiguration Config(string? legacyKey = null, string? authority = null, string? jwksUri = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:Key"] = legacyKey,
            ["Jwt:Authority"] = authority,
            ["Jwt:JwksUri"] = jwksUri,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static string Token(SigningCredentials? credentials, string issuer = Issuer, string audience = Audience)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = credentials
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Validates the way JwtBearerHandler does: configured keys + the keys fetched from JWKS.</summary>
    private static async Task<bool> IsAcceptedAsync(string token, IConfiguration config)
    {
        var parameters = ErpJwtBearerOptions.CreateValidationParameters(config);
        parameters.IssuerSigningKeys = parameters.IssuerSigningKeys.Concat(new SecurityKey[] { PublishedJwk() }).ToList();
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        return result.IsValid;
    }

    /// <summary>Identity's public key as it appears in its JWKS document (no private part).</summary>
    private static JsonWebKey PublishedJwk()
    {
        var rsa = IdentityKey.Rsa.ExportParameters(includePrivateParameters: false);
        return JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa) { KeyId = IdentityKey.KeyId });
    }

    [Fact]
    public async Task Rs256TokenSignedByIdentity_IsAccepted()
    {
        var token = Token(new SigningCredentials(IdentityKey, SecurityAlgorithms.RsaSha256));
        (await IsAcceptedAsync(token, Config())).Should().BeTrue();
    }

    [Fact]
    public async Task Rs256TokenSignedByAnotherKey_IsRejected()
    {
        var token = Token(new SigningCredentials(ForeignKey, SecurityAlgorithms.RsaSha256));
        (await IsAcceptedAsync(token, Config())).Should().BeFalse();
    }

    [Fact]
    public async Task Hs256Token_IsAcceptedOnlyWhileTheLegacyKeyIsConfigured()
    {
        var token = Token(new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(LegacyKey)), SecurityAlgorithms.HmacSha256));

        (await IsAcceptedAsync(token, Config(legacyKey: LegacyKey))).Should().BeTrue();
        (await IsAcceptedAsync(token, Config())).Should().BeFalse();
    }

    [Fact]
    public async Task Hs256TokenKeyedWithThePublicKey_IsRejected()
    {
        // Algorithm confusion: HMAC "signed" with the published public key material.
        var publicKeyBytes = Encoding.UTF8.GetBytes(PublishedJwk().N);
        var token = Token(new SigningCredentials(new SymmetricSecurityKey(publicKeyBytes), SecurityAlgorithms.HmacSha256));

        (await IsAcceptedAsync(token, Config(legacyKey: LegacyKey))).Should().BeFalse();
        (await IsAcceptedAsync(token, Config())).Should().BeFalse();
    }

    [Fact]
    public async Task UnsignedToken_IsRejected()
    {
        (await IsAcceptedAsync(Token(credentials: null), Config(legacyKey: LegacyKey))).Should().BeFalse();
    }

    [Theory]
    [InlineData("urn:ERP.Identity", Audience)]
    [InlineData("https://evil.example", Audience)]
    [InlineData(Issuer, "other-audience")]
    public async Task OtherIssuerOrAudience_IsRejected(string issuer, string audience)
    {
        var token = Token(new SigningCredentials(IdentityKey, SecurityAlgorithms.RsaSha256), issuer, audience);
        (await IsAcceptedAsync(token, Config())).Should().BeFalse();
    }

    [Fact]
    public void LegacyKey_IsOptional_AndAPublicPlaceholderIsStillRefusedOutsideDevelopment()
    {
        var act = () => ErpJwtBearerOptions.CreateValidationParameters(Config());
        act.Should().NotThrow();

        var previous = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            var placeholder = () => ErpJwtBearerOptions.CreateValidationParameters(
                Config(legacyKey: "ChangeMe_Minimum_256_Bits_Required!"));
            placeholder.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previous);
        }
    }

    [Theory]
    [InlineData(null, null, "http://identityservice:5001/.well-known/jwks.json")]
    [InlineData("http://localhost:5001/", null, "http://localhost:5001/.well-known/jwks.json")]
    [InlineData("http://ignored", "http://keys.internal/jwks", "http://keys.internal/jwks")]
    public void JwksUri_ComesFromAuthorityOrExplicitSetting(string? authority, string? jwksUri, string expected)
    {
        ErpJwtBearerOptions.ResolveJwksUri(Config(authority: authority, jwksUri: jwksUri)).Should().Be(expected);
    }

    [Fact]
    public async Task JwksRetriever_ReturnsThePublishedSigningKeys_AndNoIssuer()
    {
        var document = $"{{\"keys\":[{System.Text.Json.JsonSerializer.Serialize(new { kty = "RSA", use = "sig", kid = IdentityKey.KeyId, n = PublishedJwk().N, e = PublishedJwk().E })}]}}";

        var configuration = await new JwksConfigurationRetriever()
            .GetConfigurationAsync("http://identity/.well-known/jwks.json", new StaticDocumentRetriever(document), CancellationToken.None);

        configuration.SigningKeys.Should().ContainSingle().Which.KeyId.Should().Be(IdentityKey.KeyId);
        configuration.Issuer.Should().BeNull();
    }

    [Fact]
    public void Configure_FetchesKeysFromJwks_AndChecksRevocation()
    {
        var options = new JwtBearerOptions();
        ErpJwtBearerOptions.Configure(options, Config());

        options.ConfigurationManager.Should().BeOfType<ConfigurationManager<OpenIdConnectConfiguration>>();
        options.MapInboundClaims.Should().BeFalse();
        options.RefreshOnIssuerKeyNotFound.Should().BeTrue();
        options.Events.OnTokenValidated.Should().NotBeNull();
    }

    private sealed class StaticDocumentRetriever(string document) : IDocumentRetriever
    {
        public Task<string> GetDocumentAsync(string address, CancellationToken cancel) => Task.FromResult(document);
    }
}
