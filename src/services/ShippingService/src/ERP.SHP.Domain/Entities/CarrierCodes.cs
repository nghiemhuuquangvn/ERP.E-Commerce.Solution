namespace ERP.SHP.Domain.Entities;

/// <summary>The carrier integrations the platform knows. A <see cref="Carrier"/> row of the shop picks one through its Code.</summary>
public static class CarrierCodes
{
    public const string Ghtk = "GHTK";
    public const string ViettelPost = "VTP";
    public const string Ghn = "GHN";
    public const string JAndT = "JNT";

    public static readonly string[] All = { Ghtk, ViettelPost, Ghn, JAndT };

    /// <summary>The canonical code for what was typed (codes only, not names), or null.</summary>
    public static string? Normalize(string? code) =>
        All.FirstOrDefault(c => string.Equals(c, code?.Trim(), StringComparison.OrdinalIgnoreCase));
}
