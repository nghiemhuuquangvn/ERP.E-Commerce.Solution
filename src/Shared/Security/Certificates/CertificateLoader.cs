using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Security.Certificates;

/// <summary>
/// Picks a certificate source from <c>&lt;section&gt;:Source</c> — e.g. <c>SigningKeys</c> /
/// <c>EncryptionKeys</c> (IdentityService) or <c>ServiceAuth:SigningKeys</c> (service-to-service
/// tokens):
/// <list type="bullet">
/// <item><c>KeyVault</c> — production.</item>
/// <item><c>File</c> — local / Docker Compose (PFX, optionally generated once).</item>
/// <item><c>Ephemeral</c> — only when <paramref name="allowEphemeral"/>: an in-memory key per
/// process start, for short-lived service tokens of a single-replica service.</item>
/// </list>
/// Missing or unusable configuration stops start-up — never a silent throw-away key.
/// </summary>
public static class CertificateLoader
{
    public static CertificateSet Load(IConfiguration configuration, string section, string contentRootPath, bool isDevelopment,
        CertificatePurpose purpose = CertificatePurpose.Signing, bool allowEphemeral = false, ILogger? logger = null) =>
        CreateSource(configuration, section, contentRootPath, isDevelopment, purpose, allowEphemeral, logger).Load();

    public static ICertificateSource CreateSource(IConfiguration configuration, string section, string contentRootPath, bool isDevelopment,
        CertificatePurpose purpose = CertificatePurpose.Signing, bool allowEphemeral = false, ILogger? logger = null)
    {
        var settings = configuration.GetSection(section);
        var source = settings["Source"];

        if (string.Equals(source, "KeyVault", StringComparison.OrdinalIgnoreCase))
        {
            var vaultUri = settings["KeyVault:VaultUri"]
                ?? throw new InvalidOperationException($"Configuration value '{section}:KeyVault:VaultUri' is missing.");
            var name = settings["KeyVault:CertificateName"]
                ?? throw new InvalidOperationException($"Configuration value '{section}:KeyVault:CertificateName' is missing.");
            return new KeyVaultCertificateSource(new Uri(vaultUri), name, logger: logger);
        }

        if (string.Equals(source, "File", StringComparison.OrdinalIgnoreCase))
        {
            var path = settings["File:Path"]
                ?? throw new InvalidOperationException($"Configuration value '{section}:File:Path' is missing.");
            var autoCreate = settings.GetValue("File:AutoCreate", false);
            if (autoCreate && !isDevelopment)
                logger?.LogWarning("{Section}:File:AutoCreate is on outside Development — acceptable for a local Docker stack only; production uses Source=KeyVault.", section);

            var previous = settings.GetSection("File:PreviousPaths").Get<string[]>() ?? [];
            return new FileCertificateSource(
                Resolve(path, contentRootPath),
                settings["File:Password"],
                previous.Select(p => Resolve(p, contentRootPath)).ToList(),
                autoCreate,
                purpose,
                logger);
        }

        if (allowEphemeral && string.Equals(source, "Ephemeral", StringComparison.OrdinalIgnoreCase))
            return new EphemeralCertificateSource(purpose);

        throw new InvalidOperationException(
            $"Configuration value '{section}:Source' must be 'KeyVault' or 'File'{(allowEphemeral ? " or 'Ephemeral'" : "")} (see docs/business/Identity/SigningKeys.md).");
    }

    private static string Resolve(string path, string contentRootPath) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(contentRootPath, path));
}

/// <summary>
/// A key generated in memory at start-up. Only for short-lived tokens of a service with ONE
/// replica (Docker Compose): receivers re-fetch the JWKS on an unknown kid after a restart.
/// Several replicas would each have their own key and receivers would see only one of them —
/// use Key Vault there.
/// </summary>
public sealed class EphemeralCertificateSource : ICertificateSource
{
    private readonly CertificatePurpose _purpose;

    public EphemeralCertificateSource(CertificatePurpose purpose = CertificatePurpose.Signing) => _purpose = purpose;

    public CertificateSet Load() =>
        new(CertificateSet.CreateSelfSigned("ERP ephemeral service key", _purpose, TimeSpan.FromDays(30)), []);
}
