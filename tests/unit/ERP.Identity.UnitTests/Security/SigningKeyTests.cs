using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Security.Signing;
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
public class SigningKeyTests : IDisposable
{
    private const string LegacyKey = "a-fresh-random-test-key-of-at-least-32-bytes!";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "erp-signing-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

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

    // ── Choosing the current certificate (Key Vault versions / files) ────────────────────

    [Fact]
    public void NewestValidCertificate_Signs_OthersArePublishedForVerification()
    {
        var older = Certificate(Now.AddDays(-30), Now.AddDays(300));
        var newer = Certificate(Now.AddDays(-1), Now.AddDays(400));
        var expired = Certificate(Now.AddDays(-400), Now.AddDays(-1));
        var notYetValid = Certificate(Now.AddDays(1), Now.AddDays(500));

        var set = SigningCertificateSet.FromCandidates(new[] { older, expired, newer, notYetValid }, Now);

        set.Current.Thumbprint.Should().Be(newer.Thumbprint);
        set.Previous.Select(c => c.Thumbprint).Should().Equal(older.Thumbprint);
    }

    [Fact]
    public void CertificateWithoutPrivateKey_IsNotUsable()
    {
        var publicOnly = X509CertificateLoader.LoadCertificate(Certificate(Now.AddDays(-1), Now.AddDays(10)).Export(X509ContentType.Cert));

        var act = () => SigningCertificateSet.FromCandidates(new[] { publicOnly }, Now);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── File source (local / Docker Compose) ─────────────────────────────────────────────

    [Fact]
    public void FileSource_AutoCreate_GeneratesOnce_ThenReusesTheSameCertificate()
    {
        var path = Path.Combine(_directory, "signing.pfx");

        var first = new FileSigningCertificateSource(path, "pw", [], autoCreate: true).Load();
        var second = new FileSigningCertificateSource(path, "pw", [], autoCreate: true).Load();

        File.Exists(path).Should().BeTrue();
        second.Current.Thumbprint.Should().Be(first.Current.Thumbprint);
    }

    [Fact]
    public void FileSource_MissingFileWithoutAutoCreate_StopsStartUp()
    {
        var act = () => new FileSigningCertificateSource(Path.Combine(_directory, "missing.pfx"), null, [], autoCreate: false).Load();

        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public void FileSource_PreviousCertificates_ArePublished_CurrentStaysTheConfiguredFile()
    {
        Directory.CreateDirectory(_directory);
        var currentPath = Path.Combine(_directory, "current.pfx");
        var previousPath = Path.Combine(_directory, "previous.pfx");
        var current = Certificate(Now.AddDays(-10), Now.AddDays(300));
        var previous = Certificate(Now.AddDays(-1), Now.AddDays(30)); // newer NotBefore — still not "current"
        File.WriteAllBytes(currentPath, current.Export(X509ContentType.Pfx));
        File.WriteAllBytes(previousPath, previous.Export(X509ContentType.Pfx));

        var set = new FileSigningCertificateSource(currentPath, null, [previousPath], autoCreate: false).Load();

        set.Current.Thumbprint.Should().Be(current.Thumbprint);
        set.Previous.Select(c => c.Thumbprint).Should().Equal(previous.Thumbprint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Ephemeral")]
    public void Loader_RequiresAnExplicitSource(string? source)
    {
        var act = () => SigningCertificateLoader.CreateSource(Config(("SigningKeys:Source", source)), _directory, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SigningKeys:Source*");
    }

    [Fact]
    public void Loader_KeyVaultWithoutVaultUri_StopsStartUp()
    {
        var act = () => SigningCertificateLoader.CreateSource(
            Config(("SigningKeys:Source", "KeyVault"), ("SigningKeys:KeyVault:CertificateName", "identity-signing")), _directory, false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*VaultUri*");
    }

    [Fact]
    public void Loader_FilePathIsRelativeToTheContentRoot()
    {
        var set = SigningCertificateLoader.Load(
            Config(("SigningKeys:Source", "File"), ("SigningKeys:File:Path", "keys/signing.pfx"), ("SigningKeys:File:AutoCreate", "true")),
            _directory, isDevelopment: true);

        File.Exists(Path.Combine(_directory, "keys", "signing.pfx")).Should().BeTrue();
        set.Previous.Should().BeEmpty();
    }

    // ── Access tokens ────────────────────────────────────────────────────────────────────

    private static async Task<string> IssueAsync(SigningCertificateSet certificates, IConfiguration config)
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
        var certificates = SigningCertificateSet.FromCandidates([Certificate(Now.AddDays(-1), Now.AddDays(30))], Now);

        var token = await IssueAsync(certificates, Config());

        var jwt = new JsonWebToken(token);
        jwt.Alg.Should().Be(SecurityAlgorithms.RsaSha256);
        (await new JsonWebTokenHandler().ValidateTokenAsync(token, PublicKeyOnly(certificates.Current))).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AccessToken_KeyId_MatchesTheKeyOpenIddictPublishesInTheJwks()
    {
        var certificates = SigningCertificateSet.FromCandidates([Certificate(Now.AddDays(-1), Now.AddDays(30))], Now);
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
        var certificates = SigningCertificateSet.FromCandidates([Certificate(Now.AddDays(-1), Now.AddDays(30))], Now);

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
        var certificates = SigningCertificateSet.FromCandidates([Certificate(Now.AddDays(-1), Now.AddDays(30))], Now);

        var act = () => new AccessTokenSigningCredentials(certificates, Config(("Jwt:AccessTokenSigning", "None")));

        act.Should().Throw<InvalidOperationException>();
    }
}
