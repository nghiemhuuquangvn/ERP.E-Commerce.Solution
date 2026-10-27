using System.Security.Cryptography;
using System.Text;

namespace ERP.PAY.Infrastructure.Gateway;

/// <summary>HMAC helpers shared by the gateways. Signatures are compared in constant time.</summary>
public static class GatewaySigning
{
    public static string HmacSha256Hex(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    public static string HmacSha512Hex(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    /// <summary>Equal hex strings regardless of case; false for null / empty / different length without leaking where they differ.</summary>
    public static bool HexEquals(string? expected, string? received)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(received)) return false;
        var a = Encoding.UTF8.GetBytes(expected.ToLowerInvariant());
        var b = Encoding.UTF8.GetBytes(received.Trim().ToLowerInvariant());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>Whole VND (the only currency these gateways settle): 150000.00 → 150000.</summary>
    public static long WholeAmount(decimal amount) => (long)decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    public static bool IsVnd(string? currency) => string.Equals(currency, "VND", StringComparison.OrdinalIgnoreCase);
}
