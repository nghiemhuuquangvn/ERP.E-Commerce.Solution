using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ERP.Shared.Security.Certificates;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ERP.Shared.UnitTests.Security;

/// <summary>
/// Token signing / encryption certificates (IdentityService, service-to-service tokens):
/// choosing the current one among Key Vault versions / files, local file and in-memory sources.
/// </summary>
public class CertificateSourceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "erp-certs-" + Guid.NewGuid().ToString("N"));

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
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    // ── Choosing the current certificate (Key Vault versions / files) ────────────────────

    [Fact]
    public void NewestValidCertificate_Signs_OthersArePublishedForVerification()
    {
        var older = Certificate(Now.AddDays(-30), Now.AddDays(300));
        var newer = Certificate(Now.AddDays(-1), Now.AddDays(400));
        var expired = Certificate(Now.AddDays(-400), Now.AddDays(-1));
        var notYetValid = Certificate(Now.AddDays(1), Now.AddDays(500));

        var set = CertificateSet.FromCandidates(new[] { older, expired, newer, notYetValid }, Now);

        set.Current.Thumbprint.Should().Be(newer.Thumbprint);
        set.Previous.Select(c => c.Thumbprint).Should().Equal(older.Thumbprint);
    }

    [Fact]
    public void CertificateWithoutPrivateKey_IsNotUsable()
    {
        var publicOnly = X509CertificateLoader.LoadCertificate(Certificate(Now.AddDays(-1), Now.AddDays(10)).Export(X509ContentType.Cert));

        var act = () => CertificateSet.FromCandidates(new[] { publicOnly }, Now);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── File source (local / Docker Compose) ─────────────────────────────────────────────

    [Fact]
    public void FileSource_AutoCreate_GeneratesOnce_ThenReusesTheSameCertificate()
    {
        var path = Path.Combine(_directory, "signing.pfx");

        var first = new FileCertificateSource(path, "pw", [], autoCreate: true).Load();
        var second = new FileCertificateSource(path, "pw", [], autoCreate: true).Load();

        File.Exists(path).Should().BeTrue();
        second.Current.Thumbprint.Should().Be(first.Current.Thumbprint);
    }

    [Fact]
    public void FileSource_MissingFileWithoutAutoCreate_StopsStartUp()
    {
        var act = () => new FileCertificateSource(Path.Combine(_directory, "missing.pfx"), null, [], autoCreate: false).Load();

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

        var set = new FileCertificateSource(currentPath, null, [previousPath], autoCreate: false).Load();

        set.Current.Thumbprint.Should().Be(current.Thumbprint);
        set.Previous.Select(c => c.Thumbprint).Should().Equal(previous.Thumbprint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Ephemeral")]
    public void Loader_RequiresAnExplicitSource(string? source)
    {
        var act = () => CertificateLoader.CreateSource(Config(("SigningKeys:Source", source)), "SigningKeys", _directory, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SigningKeys:Source*");
    }

    [Fact]
    public void Loader_KeyVaultWithoutVaultUri_StopsStartUp()
    {
        var act = () => CertificateLoader.CreateSource(
            Config(("SigningKeys:Source", "KeyVault"), ("SigningKeys:KeyVault:CertificateName", "identity-signing")), "SigningKeys", _directory, false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*VaultUri*");
    }

    [Fact]
    public void Loader_FilePathIsRelativeToTheContentRoot()
    {
        var set = CertificateLoader.Load(
            Config(("SigningKeys:Source", "File"), ("SigningKeys:File:Path", "keys/signing.pfx"), ("SigningKeys:File:AutoCreate", "true")),
            "SigningKeys", _directory, isDevelopment: true);

        File.Exists(Path.Combine(_directory, "keys", "signing.pfx")).Should().BeTrue();
        set.Previous.Should().BeEmpty();
    }

    // ── Purpose, ephemeral keys ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CertificatePurpose.Signing, X509KeyUsageFlags.DigitalSignature)]
    [InlineData(CertificatePurpose.Encryption, X509KeyUsageFlags.KeyEncipherment)]
    public void GeneratedCertificates_CarryTheKeyUsageOfTheirPurpose(CertificatePurpose purpose, X509KeyUsageFlags usage)
    {
        var path = Path.Combine(_directory, purpose + ".pfx");

        var set = new FileCertificateSource(path, null, [], autoCreate: true, purpose).Load();

        set.Current.Extensions.OfType<X509KeyUsageExtension>().Single().KeyUsages.Should().Be(usage);
    }

    [Fact]
    public void Ephemeral_IsOnlyAcceptedWhereAllowed_AndGivesAFreshKeyEachLoad()
    {
        var config = Config(("ServiceAuth:SigningKeys:Source", "Ephemeral"));

        var refused = () => CertificateLoader.CreateSource(config, "ServiceAuth:SigningKeys", _directory, isDevelopment: false);
        refused.Should().Throw<InvalidOperationException>();

        var source = CertificateLoader.CreateSource(config, "ServiceAuth:SigningKeys", _directory, isDevelopment: false, allowEphemeral: true);
        source.Load().Current.Thumbprint.Should().NotBe(source.Load().Current.Thumbprint);
    }
}
