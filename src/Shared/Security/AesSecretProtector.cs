using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ERP.Shared.Security;

/// <summary>
/// AES-256-CBC symmetric encryption, key read from configuration the same way
/// AuthenticationExtensions reads "Jwt:Key" — a single shared secret, not per-user.
/// Ciphertext format: base64(IV || encrypted bytes), so no separate IV storage column
/// is needed alongside the encrypted value.
/// </summary>
public class AesSecretProtector : ISecretProtector
{
    private readonly byte[] _key;

    public AesSecretProtector(IConfiguration configuration)
    {
        var keyString = configuration["Security:SettingsEncryptionKey"]
            ?? throw new InvalidOperationException("Configuration value 'Security:SettingsEncryptionKey' is missing.");

        // SHA-256 the configured passphrase down to a valid 32-byte AES-256 key,
        // so the config value itself doesn't have to be a precisely-sized base64 blob.
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
    }

    public string Protect(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

        return Convert.ToBase64String(result);
    }

    public string Unprotect(string ciphertext)
    {
        var fullBytes = Convert.FromBase64String(ciphertext);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[aes.IV.Length];
        Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var encrypted = new byte[fullBytes.Length - iv.Length];
        Buffer.BlockCopy(fullBytes, iv.Length, encrypted, 0, encrypted.Length);

        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        return Encoding.UTF8.GetString(decrypted);
    }
}
