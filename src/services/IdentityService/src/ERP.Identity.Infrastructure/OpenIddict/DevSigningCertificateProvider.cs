using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ERP.Identity.Infrastructure.OpenIddict
{
    /// <summary>
    /// Gets-or-creates a self-signed RSA certificate for OpenIddict token signing —
    /// Part A3 of CommonServiceRoadmap.md §1 (asymmetric signing cutover, replacing
    /// A0/A1/A2's AddEphemeralSigningKey() stopgap, which regenerated a new key every
    /// process start and so orphaned every previously-issued token and any cached
    /// JWKS document on each restart).
    ///
    /// This is deliberately hand-rolled rather than OpenIddict's own
    /// AddDevelopmentSigningCertificate() helper: that method manages the certificate
    /// entirely internally (X509Store lookup/create), giving no way to retrieve the
    /// same certificate's public key afterward — and the existing "Bearer" JwtBearer
    /// scheme in Program.cs needs that exact public key added to its
    /// IssuerSigningKeys so it can keep validating OpenIddict-issued tokens now that
    /// they're RS256 instead of the old shared-HMAC-secret HS256.
    ///
    /// Still explicitly a development/local stopgap, not production PKI: persisted in
    /// the current user's certificate store under a fixed subject name (self-signed,
    /// 2-year validity, no rotation policy, no HSM/Key Vault). Real production
    /// certificate provisioning is flagged as a follow-up decision in the roadmap doc,
    /// not designed here.
    /// </summary>
    public static class DevSigningCertificateProvider
    {
        private const string SubjectName = "CN=ERP.Identity OpenIddict Dev Signing Certificate";

        public static X509Certificate2 GetOrCreate()
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            var existing = store.Certificates
                .Find(X509FindType.FindBySubjectDistinguishedName, SubjectName, validOnly: false)
                .OfType<X509Certificate2>()
                .FirstOrDefault(certificate => certificate.NotAfter > DateTime.Now && certificate.HasPrivateKey);
            if (existing is not null)
                return existing;

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(SubjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));

            // Re-import as exportable/persisted — CreateSelfSigned's result isn't
            // guaranteed to survive being added to a store with its private key intact
            // otherwise (Windows-specific quirk of ephemeral vs. persisted key sets).
            certificate = X509CertificateLoader.LoadPkcs12(
                certificate.Export(X509ContentType.Pfx),
                password: null,
                X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

            store.Add(certificate);
            return certificate;
        }
    }
}
