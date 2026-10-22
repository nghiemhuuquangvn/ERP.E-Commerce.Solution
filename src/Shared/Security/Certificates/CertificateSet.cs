using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ERP.Shared.Security.Certificates;

/// <summary>What a certificate is for — decides the key usage of generated certificates.</summary>
public enum CertificatePurpose
{
    /// <summary>Signing tokens (RS256): IdentityService access tokens, service-to-service tokens.</summary>
    Signing,

    /// <summary>Encrypting tokens (RSA-OAEP): OpenIddict refresh tokens and authorization codes.</summary>
    Encryption
}

/// <summary>
/// RSA certificates with their private keys. <see cref="Current"/> is used for new tokens;
/// <see cref="Previous"/> stay usable for verification / decryption so a rotation doesn't
/// invalidate tokens issued before it. See docs/business/Identity/SigningKeys.md.
/// </summary>
public sealed class CertificateSet
{
    public CertificateSet(X509Certificate2 current, IReadOnlyList<X509Certificate2> previous)
    {
        Current = current;
        Previous = previous;
    }

    public X509Certificate2 Current { get; }
    public IReadOnlyList<X509Certificate2> Previous { get; }
    public IEnumerable<X509Certificate2> All => new[] { Current }.Concat(Previous);

    /// <summary>
    /// Keeps the usable candidates (private key present, valid at <paramref name="now"/>);
    /// the one that became valid most recently is <see cref="Current"/>.
    /// </summary>
    public static CertificateSet FromCandidates(IEnumerable<X509Certificate2> candidates, DateTimeOffset now)
    {
        var usable = candidates
            .Where(c => IsUsable(c, now))
            .GroupBy(c => c.Thumbprint)
            .Select(g => g.First())
            .OrderByDescending(c => c.NotBefore)
            .ToList();

        if (usable.Count == 0)
            throw new InvalidOperationException(
                "No usable certificate: an RSA certificate with its private key, currently valid, is required.");

        return new CertificateSet(usable[0], usable.Skip(1).ToList());
    }

    /// <summary>RSA with its private key, and valid at <paramref name="now"/>.</summary>
    public static bool IsUsable(X509Certificate2 certificate, DateTimeOffset now)
    {
        using var rsa = certificate.HasPrivateKey ? certificate.GetRSAPrivateKey() : null;
        return rsa is not null
            && certificate.NotBefore.ToUniversalTime() <= now.UtcDateTime
            && certificate.NotAfter.ToUniversalTime() > now.UtcDateTime;
    }

    /// <summary>Self-signed RSA 2048 certificate for local stacks (File AutoCreate, Ephemeral).</summary>
    public static X509Certificate2 CreateSelfSigned(string subject, CertificatePurpose purpose, TimeSpan validity)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={subject}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            purpose == CertificatePurpose.Encryption ? X509KeyUsageFlags.KeyEncipherment : X509KeyUsageFlags.DigitalSignature,
            critical: true));
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.Add(validity));
        // Re-import so the private key is exportable/persistable on every platform.
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }
}

public interface ICertificateSource
{
    CertificateSet Load();
}
