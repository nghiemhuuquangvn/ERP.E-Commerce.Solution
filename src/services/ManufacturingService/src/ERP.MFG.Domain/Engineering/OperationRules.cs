namespace ERP.MFG.Domain.Engineering;

/// <summary>
/// The input an operation may take and when it counts as done (BR-OP-04, BR-OP-06). Pure: the caller adds the quantities of reports still on their way,
/// so two reports waiting for the Warehouse cannot together go past a limit one of them alone would respect.
/// </summary>
public static class OperationInput
{
    /// <summary>
    /// What the operation may take in total (BR-OP-04): the first operation <c>Quantity x (1 + OverProduction tolerance %)</c>; a later one the cumulative good
    /// output of the operation before it.
    /// </summary>
    public static decimal Available(bool isFirst, decimal orderQuantity, decimal overProductionPct, decimal previousGood)
        => isFirst ? Math.Round(orderQuantity * (1m + overProductionPct / 100m), 6) : previousGood;

    /// <summary>What the operation must have taken to count as done (BR-OP-06): the order quantity for the first operation, else the good output of the previous one.</summary>
    public static decimal Planned(bool isFirst, decimal orderQuantity, decimal previousGood) => isFirst ? orderQuantity : previousGood;

    /// <summary>BR-OP-06: the previous operation is done (or this is the first), there is something to take and the operation has taken all of it.</summary>
    public static bool IsComplete(bool isFirst, bool previousCompleted, decimal orderQuantity, decimal previousGood, decimal taken)
    {
        var planned = Planned(isFirst, orderQuantity, previousGood);
        return (isFirst || previousCompleted) && planned > 0 && taken >= planned;
    }
}

/// <summary>
/// Backflush (BR-MAT-08): the quantity of a material to consume when output is reported, computed on the cumulative output so rounding errors do not add up.
/// Pure: the one place the number is computed.
/// </summary>
public static class BackflushCalculator
{
    /// <summary>
    /// <c>ROUNDUP(cumulativeOutput x required / orderQuantity, decimals) - alreadyConsumed</c>, never below zero. <paramref name="alreadyConsumed"/> is what the order
    /// consumed plus what backflush documents still on their way will consume.
    /// </summary>
    public static decimal Quantity(decimal cumulativeOutput, decimal required, decimal orderQuantity, int decimals, decimal alreadyConsumed)
    {
        if (orderQuantity <= 0 || cumulativeOutput <= 0 || required <= 0) return 0m;
        var target = RoundUp(cumulativeOutput * required / orderQuantity, decimals);
        return Math.Max(target - alreadyConsumed, 0m);
    }

    /// <summary>Rounds up to <paramref name="decimals"/> places; noise below 8 places is dropped first so an exact value is not pushed up by division residue.</summary>
    public static decimal RoundUp(decimal value, int decimals)
    {
        var places = Math.Clamp(decimals, 0, 6);
        var factor = (decimal)Math.Pow(10, places);
        return Math.Ceiling(Math.Round(value, 8) * factor) / factor;
    }
}
