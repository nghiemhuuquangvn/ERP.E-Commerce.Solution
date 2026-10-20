using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace ERP.Identity.Infrastructure.Security.Signing
{
    /// <summary>
    /// Signing certificates from PFX files — local development and the Docker Compose stack
    /// (no Key Vault there). Production uses <see cref="KeyVaultSigningCertificateSource"/>.
    ///
    /// <c>SigningKeys:File:Path</c> is the current certificate; <c>SigningKeys:File:PreviousPaths</c>
    /// are older ones still published for verification. With <c>AutoCreate</c> a missing or
    /// expired current certificate is generated (self-signed RSA 2048, 2 years) — only for
    /// local stacks; the file must live on a persistent volume or every restart rotates the key.
    /// </summary>
    public sealed class FileSigningCertificateSource : ISigningCertificateSource
    {
        private readonly string _path;
        private readonly string? _password;
        private readonly IReadOnlyList<string> _previousPaths;
        private readonly bool _autoCreate;
        private readonly ILogger? _logger;

        public FileSigningCertificateSource(string path, string? password, IReadOnlyList<string> previousPaths, bool autoCreate, ILogger? logger = null)
        {
            _path = path;
            _password = string.IsNullOrEmpty(password) ? null : password;
            _previousPaths = previousPaths;
            _autoCreate = autoCreate;
            _logger = logger;
        }

        public SigningCertificateSet Load()
        {
            var now = DateTimeOffset.UtcNow;
            var current = File.Exists(_path) ? LoadFile(_path) : null;

            if (current is null || current.NotAfter.ToUniversalTime() <= now.UtcDateTime)
            {
                if (!_autoCreate)
                    throw new InvalidOperationException(current is null
                        ? $"Token signing certificate '{_path}' not found (SigningKeys:File:Path). Provide it or set SigningKeys:File:AutoCreate for a local stack."
                        : $"Token signing certificate '{_path}' has expired.");

                current = CreateAndSave(_path, _password);
                _logger?.LogWarning("Generated a new self-signed token signing certificate at {Path} (thumbprint {Thumbprint}). Local stacks only — production uses Key Vault.",
                    _path, current.Thumbprint);
            }

            if (!SigningCertificateSet.IsUsable(current, now))
                throw new InvalidOperationException($"Token signing certificate '{_path}' is not usable (RSA with private key, valid now).");

            var previous = _previousPaths
                .Where(File.Exists)
                .Select(LoadFile)
                .Where(c => c.Thumbprint != current.Thumbprint && SigningCertificateSet.IsUsable(c, now))
                .ToList();
            return new SigningCertificateSet(current, previous);
        }

        private X509Certificate2 LoadFile(string path) =>
            X509CertificateLoader.LoadPkcs12FromFile(path, _password, X509KeyStorageFlags.Exportable);

        private static X509Certificate2 CreateAndSave(string path, string? password)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=ERP.Identity token signing", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var pfx = certificate.Export(X509ContentType.Pfx, password);
            File.WriteAllBytes(path, pfx);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            return X509CertificateLoader.LoadPkcs12(pfx, password, X509KeyStorageFlags.Exportable);
        }
    }
}
