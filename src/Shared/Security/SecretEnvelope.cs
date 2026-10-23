namespace ERP.Shared.Security;

/// <summary>
/// Marks a value as "encrypted by <see cref="ISecretProtector"/>" so a reader can tell it from a legacy plaintext one.
/// <see cref="AesSecretProtector"/> output is plain base64 with no marker, so without the prefix a value written
/// before encryption was introduced could not be told apart from ciphertext and would fail to decrypt.
/// Used where a secret has to sit in a column or a message that older rows/messages already filled with plaintext
/// (Identity outbox payloads, webhook secrets).
/// </summary>
public static class SecretEnvelope
{
    public const string Prefix = "enc:v1:";

    /// <summary>What replaces a secret in a stored message once it has been delivered.</summary>
    public const string Redacted = "[redacted]";

    public static bool IsSealed(string? value) => value != null && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Encrypts <paramref name="plaintext"/>; an already sealed value is returned unchanged.</summary>
    public static string Seal(ISecretProtector protector, string plaintext) =>
        IsSealed(plaintext) ? plaintext : Prefix + protector.Protect(plaintext);

    /// <summary>Decrypts a sealed value; anything without the prefix is a legacy plaintext and is returned as it is.</summary>
    public static string Open(ISecretProtector protector, string value) =>
        IsSealed(value) ? protector.Unprotect(value[Prefix.Length..]) : value;
}
