namespace ERP.MFG.Domain.Engineering;

/// <summary>
/// The quantity rules of material issue and return (BR-MAT-02, BR-MAT-05), pure so they can be tested without a store. Pending documents count: two documents waiting
/// for the Warehouse cannot together go past a limit that one of them alone would respect.
/// </summary>
public static class MaterialLimits
{
    /// <summary>The most a line may have net issued without the over-issue permission: <c>Required x (1 + tolerance %)</c>.</summary>
    public static decimal MaxNetIssue(decimal required, decimal tolerancePct) => Math.Round(required * (1m + tolerancePct / 100m), 6);

    /// <summary>How much more may be issued within the tolerance (never negative).</summary>
    public static decimal RemainingWithinTolerance(decimal required, decimal tolerancePct, decimal netIssued, decimal pendingIssue)
        => Math.Max(MaxNetIssue(required, tolerancePct) - netIssued - pendingIssue, 0m);

    /// <summary>True when issuing <paramref name="qty"/> more takes the line past <see cref="MaxNetIssue"/> (BR-MAT-02).</summary>
    public static bool ExceedsTolerance(decimal required, decimal tolerancePct, decimal netIssued, decimal pendingIssue, decimal qty)
        => netIssued + pendingIssue + qty > MaxNetIssue(required, tolerancePct);

    /// <summary>The most that may still be returned: what is out on the shop floor minus returns already waiting (never negative).</summary>
    public static decimal MaxReturn(decimal issued, decimal returned, decimal pendingReturn) => Math.Max(issued - returned - pendingReturn, 0m);

    /// <summary>
    /// The unit cost a return is booked at: the weighted average of the issue lines it gives back (BR-MAT-05 keeps the lot; the cost follows the issues).
    /// Issues without a known cost are left out; with none known the Warehouse values the return at its own cost.
    /// </summary>
    public static decimal? AverageIssueCost(IEnumerable<(decimal Qty, decimal? UnitCost)> issues)
    {
        var costed = issues.Where(i => i.UnitCost.HasValue && i.Qty > 0).ToList();
        var qty = costed.Sum(i => i.Qty);
        return qty == 0 ? null : Math.Round(costed.Sum(i => i.Qty * i.UnitCost!.Value) / qty, 6);
    }
}
