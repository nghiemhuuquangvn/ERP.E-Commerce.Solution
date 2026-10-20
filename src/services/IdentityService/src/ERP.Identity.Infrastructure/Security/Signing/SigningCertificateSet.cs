using System.Security.Cryptography.X509Certificates;

namespace ERP.Identity.Infrastructure.Security.Signing
{
    /// <summary>
    /// The RS256 certificates IdentityService signs with. <see cref="Current"/> signs every new
    /// token (login tokens and OpenIddict tokens); <see cref="Previous"/> are still published in
    /// the JWKS so tokens signed before a rotation stay valid until they expire.
    /// See docs/business/Identity/SigningKeys.md.
    /// </summary>
    public sealed class SigningCertificateSet
    {
        public SigningCertificateSet(X509Certificate2 current, IReadOnlyList<X509Certificate2> previous)
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
        public static SigningCertificateSet FromCandidates(IEnumerable<X509Certificate2> candidates, DateTimeOffset now)
        {
            var usable = candidates
                .Where(c => IsUsable(c, now))
                .GroupBy(c => c.Thumbprint)
                .Select(g => g.First())
                .OrderByDescending(c => c.NotBefore)
                .ToList();

            if (usable.Count == 0)
                throw new InvalidOperationException(
                    "No usable token signing certificate: an RSA certificate with its private key, currently valid, is required.");

            return new SigningCertificateSet(usable[0], usable.Skip(1).ToList());
        }

        /// <summary>RSA with its private key, and valid at <paramref name="now"/>.</summary>
        public static bool IsUsable(X509Certificate2 certificate, DateTimeOffset now)
        {
            using var rsa = certificate.HasPrivateKey ? certificate.GetRSAPrivateKey() : null;
            return rsa is not null
                && certificate.NotBefore.ToUniversalTime() <= now.UtcDateTime
                && certificate.NotAfter.ToUniversalTime() > now.UtcDateTime;
        }
    }

    public interface ISigningCertificateSource
    {
        SigningCertificateSet Load();
    }
}
