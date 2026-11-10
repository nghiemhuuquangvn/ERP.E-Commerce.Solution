using System.Text.RegularExpressions;

namespace ERP.MFG.Domain.Engineering;

/// <summary>
/// The lot number of a finished good (BR-FG-02): the plant's format with <c>{ItemCode}</c>, <c>{yyMMdd}</c>, <c>{yyyyMMdd}</c> and <c>{seqN}</c> (N digits, e.g.
/// <c>{seq2}</c>). The sequence is the smallest free one of that item and day, so same-day receipts get -01, -02 ... Pure.
/// </summary>
public static class LotNumberFormatter
{
    private static readonly Regex SeqToken = new(@"\{seq(\d)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Format(string format, string itemCode, DateOnly date, int sequence)
    {
        var text = format.Replace("{ItemCode}", itemCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{yyyyMMdd}", date.ToString("yyyyMMdd"), StringComparison.Ordinal)
            .Replace("{yyMMdd}", date.ToString("yyMMdd"), StringComparison.Ordinal);
        if (SeqToken.IsMatch(text)) return SeqToken.Replace(text, m => sequence.ToString("D" + m.Groups[1].Value));
        return sequence <= 1 ? text : $"{text}-{sequence:D2}";   // a format without a sequence token only needs one after the first
    }

    /// <summary>The first number of the format that is not in <paramref name="taken"/> (compared without case).</summary>
    public static string Next(string format, string itemCode, DateOnly date, ISet<string> taken)
    {
        for (var sequence = 1; sequence < 100_000; sequence++)
        {
            var candidate = Format(format, itemCode, date, sequence);
            if (!taken.Contains(candidate)) return candidate;
        }
        throw ERP.MFG.Domain.Common.MfgRules.Invalid("No free lot number is left for this item and day.");
    }
}

/// <summary>How much finished product an order may receive (BR-FG-01, BR-FG-06, BR-PO-17, BR-OP-09). Pure.</summary>
public static class ReceiptLimits
{
    /// <summary>The main product: <c>Quantity x (1 + over-production tolerance)</c>, and - when the order has operations - never more than the good output of the last one.</summary>
    public static decimal MainCeiling(decimal orderQuantity, decimal overProductionPct, decimal? lastOperationGood)
    {
        var tolerance = Math.Round(orderQuantity * (1m + overProductionPct / 100m), 6);
        return lastOperationGood is { } good ? Math.Min(tolerance, good) : tolerance;
    }

    /// <summary>A co- or by-product: <c>Planned x (1 + tolerance)</c> (BR-FG-06).</summary>
    public static decimal OutputCeiling(decimal planned, decimal overProductionPct) => Math.Round(planned * (1m + overProductionPct / 100m), 6);

    /// <summary>What may still be received (never negative).</summary>
    public static decimal Remaining(decimal ceiling, decimal received, decimal pending) => Math.Max(ceiling - received - pending, 0m);
}

/// <summary>When an order may be completed without a short-close (BR-PO-14). Pure.</summary>
public static class OrderCompletion
{
    /// <summary><c>CompletedQty &gt;= Quantity x (1 - under-completion tolerance)</c>.</summary>
    public static bool MeetsThreshold(decimal completed, decimal orderQuantity, decimal underCompletionPct)
        => completed >= Math.Round(orderQuantity * (1m - underCompletionPct / 100m), 6);
}

/// <summary>One condition of the close checklist (BR-PO-16).</summary>
public sealed record CloseCondition(string Code, bool Met, string Message);
