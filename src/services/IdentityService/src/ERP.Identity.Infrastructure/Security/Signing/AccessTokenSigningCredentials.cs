using ERP.Shared.Security.Certificates;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Identity.Infrastructure.Security.Signing
{
    /// <summary>
    /// What TokenService signs login access tokens with: RS256 with the current signing
    /// certificate. Services validate with its public key from Identity's JWKS; the key id is
    /// the one OpenIddict publishes for this certificate. See docs/business/Identity/SigningKeys.md.
    /// </summary>
    public sealed class AccessTokenSigningCredentials
    {
        public AccessTokenSigningCredentials(CertificateSet certificates)
        {
            Credentials = new X509SigningCredentials(certificates.Current, SecurityAlgorithms.RsaSha256);
        }

        public SigningCredentials Credentials { get; }
    }
}
