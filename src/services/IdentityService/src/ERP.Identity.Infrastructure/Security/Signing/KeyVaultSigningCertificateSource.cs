using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Microsoft.Extensions.Logging;

namespace ERP.Identity.Infrastructure.Security.Signing
{
    /// <summary>
    /// Production signing certificates from Azure Key Vault (<c>SigningKeys:KeyVault:VaultUri</c>,
    /// <c>SigningKeys:KeyVault:CertificateName</c>), authenticated with
    /// <see cref="DefaultAzureCredential"/> (managed / workload identity — no secret in config).
    ///
    /// Rotation: every enabled, currently valid version is loaded; the newest signs, older ones
    /// stay in the JWKS. Create a new version (or let Key Vault auto-renew), restart Identity,
    /// and disable the old version once tokens signed with it have expired (≥ Jwt:Expired).
    /// The identity needs certificates/list and secrets/get (the private key is a secret).
    /// </summary>
    public sealed class KeyVaultSigningCertificateSource : ISigningCertificateSource
    {
        private readonly CertificateClient _client;
        private readonly string _certificateName;
        private readonly ILogger? _logger;

        public KeyVaultSigningCertificateSource(Uri vaultUri, string certificateName, TokenCredential? credential = null, ILogger? logger = null)
        {
            _client = new CertificateClient(vaultUri, credential ?? new DefaultAzureCredential());
            _certificateName = certificateName;
            _logger = logger;
        }

        public SigningCertificateSet Load()
        {
            var now = DateTimeOffset.UtcNow;
            var candidates = new List<X509Certificate2>();

            foreach (var version in _client.GetPropertiesOfCertificateVersions(_certificateName))
            {
                if (version.Enabled != true || version.NotBefore > now || version.ExpiresOn <= now)
                    continue;

                candidates.Add(_client.DownloadCertificate(
                    new DownloadCertificateOptions(_certificateName)
                    {
                        Version = version.Version,
                        KeyStorageFlags = X509KeyStorageFlags.Exportable
                    }).Value);
            }

            var set = SigningCertificateSet.FromCandidates(candidates, now);
            _logger?.LogInformation(
                "Token signing certificate {Name}: current {Thumbprint}, {PreviousCount} previous version(s) published for verification.",
                _certificateName, set.Current.Thumbprint, set.Previous.Count);
            return set;
        }
    }
}
