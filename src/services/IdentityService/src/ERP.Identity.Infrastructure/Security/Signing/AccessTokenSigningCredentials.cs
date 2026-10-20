using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Identity.Infrastructure.Security.Signing
{
    /// <summary>
    /// What TokenService signs login access tokens with.
    ///
    /// <c>Jwt:AccessTokenSigning</c>: <c>Rsa</c> (default) = RS256 with the current signing
    /// certificate — services validate with its public key from Identity's JWKS.
    /// <c>Hmac</c> = the legacy shared Jwt:Key (HS256), only for a rolling deployment of the
    /// cutover: keep it until every service runs the JWKS-capable release, then switch to Rsa.
    /// See docs/business/Identity/SigningKeys.md.
    /// </summary>
    public sealed class AccessTokenSigningCredentials
    {
        public AccessTokenSigningCredentials(SigningCertificateSet certificates, IConfiguration configuration)
        {
            var mode = configuration["Jwt:AccessTokenSigning"];
            if (string.IsNullOrEmpty(mode) || string.Equals(mode, "Rsa", StringComparison.OrdinalIgnoreCase))
            {
                // Same key id as OpenIddict publishes for this certificate in the JWKS.
                Credentials = new X509SigningCredentials(certificates.Current, SecurityAlgorithms.RsaSha256);
            }
            else if (string.Equals(mode, "Hmac", StringComparison.OrdinalIgnoreCase))
            {
                var key = configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Jwt:AccessTokenSigning=Hmac requires 'Jwt:Key'.");
                Credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
            }
            else
            {
                throw new InvalidOperationException($"Configuration value 'Jwt:AccessTokenSigning' must be 'Rsa' or 'Hmac', not '{mode}'.");
            }
        }

        public SigningCredentials Credentials { get; }
    }
}
