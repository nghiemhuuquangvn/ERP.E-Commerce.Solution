using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Identity.Infrastructure.Security.Signing
{
    /// <summary>
    /// Picks the signing certificate source from <c>SigningKeys:Source</c>: <c>KeyVault</c>
    /// (production) or <c>File</c> (local / Docker Compose). Missing or unusable configuration
    /// stops start-up — Identity never falls back to a throw-away key.
    /// </summary>
    public static class SigningCertificateLoader
    {
        public static SigningCertificateSet Load(IConfiguration configuration, string contentRootPath, bool isDevelopment, ILogger? logger = null) =>
            CreateSource(configuration, contentRootPath, isDevelopment, logger).Load();

        public static ISigningCertificateSource CreateSource(IConfiguration configuration, string contentRootPath, bool isDevelopment, ILogger? logger = null)
        {
            var section = configuration.GetSection("SigningKeys");
            var source = section["Source"];

            if (string.Equals(source, "KeyVault", StringComparison.OrdinalIgnoreCase))
            {
                var vaultUri = section["KeyVault:VaultUri"]
                    ?? throw new InvalidOperationException("Configuration value 'SigningKeys:KeyVault:VaultUri' is missing.");
                var name = section["KeyVault:CertificateName"]
                    ?? throw new InvalidOperationException("Configuration value 'SigningKeys:KeyVault:CertificateName' is missing.");
                return new KeyVaultSigningCertificateSource(new Uri(vaultUri), name, logger: logger);
            }

            if (string.Equals(source, "File", StringComparison.OrdinalIgnoreCase))
            {
                var path = section["File:Path"]
                    ?? throw new InvalidOperationException("Configuration value 'SigningKeys:File:Path' is missing.");
                var autoCreate = section.GetValue("File:AutoCreate", false);
                if (autoCreate && !isDevelopment)
                    logger?.LogWarning("SigningKeys:File:AutoCreate is on outside Development — acceptable for a local Docker stack only; production uses SigningKeys:Source=KeyVault.");

                var previous = section.GetSection("File:PreviousPaths").Get<string[]>() ?? [];
                return new FileSigningCertificateSource(
                    Resolve(path, contentRootPath),
                    section["File:Password"],
                    previous.Select(p => Resolve(p, contentRootPath)).ToList(),
                    autoCreate,
                    logger);
            }

            throw new InvalidOperationException(
                "Configuration value 'SigningKeys:Source' must be 'KeyVault' or 'File' (see docs/business/Identity/SigningKeys.md).");
        }

        private static string Resolve(string path, string contentRootPath) =>
            Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(contentRootPath, path));
    }
}
