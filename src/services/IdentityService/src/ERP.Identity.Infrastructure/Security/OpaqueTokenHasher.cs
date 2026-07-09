using System.Security.Cryptography;
using System.Text;

namespace ERP.Identity.Infrastructure.Security;

/// <summary>
/// Hashes high-entropy, single-use, machine-generated opaque tokens (password-reset
/// links, email-confirm links) before they're persisted, so DB read access alone
/// doesn't hand out a usable token — unlike user passwords, these need a fast,
/// deterministic lookup by exact value, so a salted PBKDF2-style hash (IPasswordHasher)
/// doesn't fit; a plain SHA-256 digest does, since the input already has 256 bits of
/// entropy and is never reused or guessed.
/// </summary>
public static class OpaqueTokenHasher
{
    public static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
