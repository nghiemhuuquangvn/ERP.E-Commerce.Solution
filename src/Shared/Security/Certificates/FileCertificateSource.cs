using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Security.Certificates;

/// <summary>
/// Certificates from PFX files — local development and the Docker Compose stack (no Key
/// Vault there). Production uses <see cref="KeyVaultCertificateSource"/>.
///
/// <c>File:Path</c> is the current certificate; <c>File:PreviousPaths</c> are older ones still
/// usable for verification / decryption. With <c>AutoCreate</c> a missing or expired current
/// certificate is generated (self-signed RSA 2048, 2 years) — only for local stacks; the file
/// must live on a persistent volume or every restart rotates the key.
/// </summary>
public sealed class FileCertificateSource : ICertificateSource
{
    private readonly string _path;
    private readonly string? _password;
    private readonly IReadOnlyList<string> _previousPaths;
    private readonly bool _autoCreate;
    private readonly CertificatePurpose _purpose;
    private readonly ILogger? _logger;

    public FileCertificateSource(string path, string? password, IReadOnlyList<string> previousPaths, bool autoCreate,
        CertificatePurpose purpose = CertificatePurpose.Signing, ILogger? logger = null)
    {
        _path = path;
        _password = string.IsNullOrEmpty(password) ? null : password;
        _previousPaths = previousPaths;
        _autoCreate = autoCreate;
        _purpose = purpose;
        _logger = logger;
    }

    public CertificateSet Load()
    {
        var now = DateTimeOffset.UtcNow;
        var current = File.Exists(_path) ? LoadFile(_path) : null;

        if (current is null || current.NotAfter.ToUniversalTime() <= now.UtcDateTime)
        {
            if (!_autoCreate)
                throw new InvalidOperationException(current is null
                    ? $"Certificate '{_path}' not found (File:Path). Provide it or set File:AutoCreate for a local stack."
                    : $"Certificate '{_path}' has expired.");

            current = CreateAndSave();
            _logger?.LogWarning("Generated a new self-signed {Purpose} certificate at {Path} (thumbprint {Thumbprint}). Local stacks only — production uses Key Vault.",
                _purpose, _path, current.Thumbprint);
        }

        if (!CertificateSet.IsUsable(current, now))
            throw new InvalidOperationException($"Certificate '{_path}' is not usable (RSA with private key, valid now).");

        var previous = _previousPaths
            .Where(File.Exists)
            .Select(LoadFile)
            .Where(c => c.Thumbprint != current.Thumbprint && CertificateSet.IsUsable(c, now))
            .ToList();
        return new CertificateSet(current, previous);
    }

    private X509Certificate2 LoadFile(string path) =>
        X509CertificateLoader.LoadPkcs12FromFile(path, _password, X509KeyStorageFlags.Exportable);

    private X509Certificate2 CreateAndSave()
    {
        using var created = CertificateSet.CreateSelfSigned(
            _purpose == CertificatePurpose.Encryption ? "ERP token encryption" : "ERP token signing", _purpose, TimeSpan.FromDays(730));

        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var pfx = created.Export(X509ContentType.Pfx, _password);
        File.WriteAllBytes(_path, pfx);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        return X509CertificateLoader.LoadPkcs12(pfx, _password, X509KeyStorageFlags.Exportable);
    }
}
