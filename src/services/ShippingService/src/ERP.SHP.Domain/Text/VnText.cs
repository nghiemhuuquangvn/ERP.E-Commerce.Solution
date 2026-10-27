using System.Globalization;
using System.Text;

namespace ERP.SHP.Domain.Text;

/// <summary>
/// Vietnamese place names written by people ("TP. Hồ Chí Minh", "Thành phố Hồ Chí Minh", "ho chi minh") must compare equal. Used by the selection
/// rules (province match) and by Viettel Post's name to numeric id matching.
/// </summary>
public static class VnText
{
    private static readonly string[] Prefixes =
    {
        "thanh pho ", "tp. ", "tp.", "tp ", "tinh ", "quan ", "huyen ", "thi xa ", "thi tran ", "phuong ", "xa ", "q. ", "h. ", "p. ", "t. ",
    };

    /// <summary>Lower case, no diacritics (đ becomes d), punctuation to spaces, one administrative prefix removed, single spaces.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) || c == '.' ? c : ' ');
        }

        var text = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        foreach (var prefix in Prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length > prefix.Length)
            {
                text = text[prefix.Length..];
                break;
            }
        }
        return text.Trim('.', ' ');
    }

    public static bool Same(string? a, string? b)
    {
        var x = Normalize(a);
        return x.Length > 0 && x == Normalize(b);
    }
}
