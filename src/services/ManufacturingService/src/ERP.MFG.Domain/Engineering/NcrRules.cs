using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Domain.Engineering;

/// <summary>The rules of a non-conformance report that need no data (BR-NCR-02 / 03). Pure.</summary>
public static class NcrRules
{
    /// <summary>
    /// Checks the disposition lines: at least one, each quantity above 0 with at most six decimals, a Scrap line names its reason, the sum is the non-conforming quantity
    /// (<c>MFG_NCR_001</c>), UseAsIs needs the approval right (<c>MFG_NCR_002</c>) and in-process output cannot be reworked (<c>MFG_NCR_003</c>).
    /// </summary>
    public static void CheckDispositions(IReadOnlyList<DispositionInput> lines, decimal nonconformingQty, bool inProcess, bool canApproveDeviation)
    {
        if (lines.Count == 0) throw MfgRules.Invalid("Add at least one disposition line.");
        foreach (var l in lines)
        {
            if (l.Qty <= 0 || Math.Round(l.Qty, 6) != l.Qty) throw MfgRules.Invalid("The quantity of a disposition line must be greater than 0.");
            if (l.Type == DispositionType.Scrap && (l.ReasonCodeId is null || l.ReasonCodeId == Guid.Empty)) throw MfgRules.Invalid("Choose a scrap reason for the Scrap line.");
            MfgRules.ThrowIf(l.Type == DispositionType.UseAsIs && !canApproveDeviation, MfgErrors.Ncr002);
            MfgRules.ThrowIf(l.Type == DispositionType.Rework && inProcess, MfgErrors.Ncr003);
        }
        MfgRules.ThrowIf(lines.Sum(l => l.Qty) != nonconformingQty, MfgErrors.Ncr001, ("qty", nonconformingQty));
    }

    /// <summary>
    /// BR-RW-02: the lot number of reworked goods is the root lot number plus <c>-R&lt;n&gt;</c>, n the smallest free number. A lot that is itself a rework lot
    /// (<c>X-R1</c>) is reworked into <c>X-R2</c>, never <c>X-R1-R1</c>.
    /// </summary>
    public static string ReworkLotNumber(string sourceLotNumber, ISet<string> taken)
    {
        var root = RootLotNumber(sourceLotNumber);
        for (var n = 1; n < 10_000; n++)
        {
            var candidate = $"{root}-R{n}";
            if (!taken.Contains(candidate)) return candidate;
        }
        throw MfgRules.Invalid("No free rework lot number is left for this lot.");
    }

    /// <summary>The lot number without trailing rework suffixes: <c>X-R2</c> -> <c>X</c>.</summary>
    public static string RootLotNumber(string lotNumber)
    {
        var root = lotNumber.Trim();
        while (System.Text.RegularExpressions.Regex.IsMatch(root, @"-R\d+$")) root = System.Text.RegularExpressions.Regex.Replace(root, @"-R\d+$", "");
        return root;
    }
}
