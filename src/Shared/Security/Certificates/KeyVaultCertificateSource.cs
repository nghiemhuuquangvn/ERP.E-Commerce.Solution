using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Security.Certificates;

/// <summary>
/// Production certificates from Azure Key Vault (<c>KeyVault:VaultUri</c>,
/// <c>KeyVault:CertificateName</c>), authenticated with <see cref="DefaultAzureCredential"/>
/// (managed / workload identity — no secret in config).
///
/// Rotation: every enabled, currently valid version is loaded; the newest is current, older
/// ones stay usable. Create a new version (or let Key Vault auto-renew), restart the service,
/// and disable the old version once tokens made with it have expired. The identity needs
/// certificates/list and secrets/get (the private key is a secret).
/// </summary>
public sealed class KeyVaultCertificateSource : ICertificateSource
{
    private readonly CertificateClient _client;
    private readonly string _certificateName;
    private readonly ILogger? _logger;

    public KeyVaultCertificateSource(Uri vaultUri, string certificateName, TokenCredential? credential = null, ILogger? logger = null)
    {
        _client = new CertificateClient(vaultUri, credential ?? new DefaultAzureCredential());
        _certificateName = certificateName;
        _logger = logger;
    }

    public CertificateSet Load()
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

        var set = CertificateSet.FromCandidates(candidates, now);
        _logger?.LogInformation(
            "Certificate {Name}: current {Thumbprint}, {PreviousCount} previous version(s) still accepted.",
            _certificateName, set.Current.Thumbprint, set.Previous.Count);
        return set;
    }
}
