namespace ERP.Shared.Security;

/// <summary>
/// Encrypts/decrypts secret-bearing settings fields (payment gateway API keys, SMTP
/// credentials, etc.) before they're persisted — these must never be stored as
/// plaintext in a settings table.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
