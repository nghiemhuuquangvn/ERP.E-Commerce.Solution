using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Security.Signing;
using ERP.Shared.Security.Certificates;
using ERP.Identity.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OpenIddict.Server;
using Xunit;

namespace ERP.Identity.UnitTests.Security;

/// <summary>
/// RS256 cutover (phase 4): Identity signs with a certificate whose private key only it holds;
/// other services validate with the public key from the JWKS OpenIddict publishes.
/// </summary>
public class SigningKeyTests
{
    private const string LegacyKey = "a-fresh-random-test-key-of-at-least-32-bytes!";
    private static X509Certificate2 Certificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test signing", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(notBefore, notAfter);
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "ERP.Identity",
                ["Jwt:Audience"] = "ERP.Services",
                ["Jwt:Expired"] = "60",
                ["Jwt:Key"] = LegacyKey,
            }.Concat(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))))
            .Build();

    // ── Access tokens ────────────────────────────────────────────────────────────────────

    private static async Task<string> IssueAsync(CertificateSet certificates, IConfiguration config)
    {
        var service = new TokenService(config, new AccessTokenSigningCredentials(certificates, config), Mock.Of<IRefreshTokenRepository>());
        var tenant = AuthTenant.Create("ACME", "Acme");
        var user = User.Create("u@example.test", "u@example.test", "hash", tenant.Id, UserType.Customer);
        return await service.GenerateAccessToken(user, tenant, ["Admin"], ["sales.read"], ["SO"]);
    }

    private static TokenValidationParameters PublicKeyOnly(X509Certificate2 certificate) => new()
    {
        ValidIssuer = "ERP.Identity",
        ValidAudience = "ERP.Services",
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        IssuerSigningKey = new X509SecurityKey(X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert)))
    };

    [Fact]
    public async Task AccessToken_IsRs256_VerifiableWithThePublicKeyAlone()
    {
        var certificates = CertificateSet.FromCandidates([Certificate(Now.AddDays(-1), Now.AddDays(30))], Now);

        var token = await IssueAsync(certificates, Config());

        var jwt = new JsonWebToken(token);
        jwt.Alg.Should().Be(SecurityAlgorithms.RsaSha256);
        (await new JsonWebTokenHandler().ValidateTokenAsync(token, PublicKeyOnly(certificates.Current))).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AccessToken_KeyId_MatchesTheKeyOpenIddictPublishesInTheJwks()
    {
        var certificates = CertificateSet.FromCandidates([Certificate(Now.AddDays(-1), Now.AddDays(30))], Now);
        var services = new ServiceCollection();
        services.AddOpenIddict().AddServer(options => options.AddSigningCertificate(certificates.Current));
        var openIddict = new OpenIddictServerOptions();
        foreach (var configure in services.BuildServiceProvider().GetServices<IConfigureOptions<OpenIddictServerOptions>>())
            configure.Configure(openIddict);

        var token = await IssueAsync(certificates, Config());

        new JsonWebToken(token).Kid.Should().Be(openIddict.SigningCredentials.Single().Key.KeyId);
    }

    [Fact]
    public async Task HmacMode_IsTheLegacyHs256_ForTheRollingCutoverOnly()
    {
        var certificates = CertificateSet.FromCandidates([Certificate(Now.AddDays(-1), Now.AddDays(30))], Now);

        var token = await IssueAsync(certificates, Config(("Jwt:AccessTokenSigning", "Hmac")));

        new JsonWebToken(token).Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        (await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "ERP.Identity",
            ValidAudience = "ERP.Services",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(LegacyKey))
        })).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UnknownSigningMode_StopsStartUp()
    {
        var certificates = CertificateSet.FromCandidates([Certificate(Now.AddDays(-1), Now.AddDays(30))], Now);

        var act = () => new AccessTokenSigningCredentials(certificates, Config(("Jwt:AccessTokenSigning", "None")));

        act.Should().Throw<InvalidOperationException>();
    }
}
