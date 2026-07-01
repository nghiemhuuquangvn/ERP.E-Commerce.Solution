using ERP.Identity.Domain.Common;
using System;
using System.Security.Cryptography;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Tracks a single stateful OAuth2 Authorization Code + PKCE flow (used for Twitter,
    /// which — unlike Google/Microsoft/Facebook — needs a redirect round trip rather than
    /// a single-shot id-token verify).
    /// </summary>
    public class OAuthState : BaseEntity
    {
        public string State { get; private set; } = default!;
        public string CodeVerifier { get; private set; } = default!;
        public string TenantCode { get; private set; } = default!;
        public DateTime ExpiredAt { get; private set; }

        protected OAuthState() { }

        public static OAuthState Create(string tenantCode, int expiryMinutes, out string codeChallenge)
        {
            if (string.IsNullOrWhiteSpace(tenantCode))
                throw new ArgumentException("TenantCode is required", nameof(tenantCode));

            var state = GenerateUrlSafeRandom(32);
            var codeVerifier = GenerateUrlSafeRandom(64);
            codeChallenge = ComputeS256Challenge(codeVerifier);

            return new OAuthState
            {
                Id = Guid.NewGuid(),
                State = state,
                CodeVerifier = codeVerifier,
                TenantCode = tenantCode,
                ExpiredAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public bool IsExpired => DateTime.UtcNow >= ExpiredAt;

        private static string GenerateUrlSafeRandom(int byteLength)
        {
            var bytes = RandomNumberGenerator.GetBytes(byteLength);
            return Base64UrlEncode(bytes);
        }

        private static string ComputeS256Challenge(string codeVerifier)
        {
            var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
