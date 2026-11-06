using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Domain.Engineering;

/// <summary>A unit of measure as MasterData reports it (a unit without a base unit is its own base, factor 1).</summary>
public sealed record UnitFacts(Guid Id, string Code, int DecimalPlaces, Guid? BaseUnitId, decimal ConversionFactor);

/// <summary>A conversion kept by ProductService for one product: 1 From = Factor To.</summary>
public sealed record ProductConversionFacts(Guid ProductId, Guid FromUomId, Guid ToUomId, decimal Factor);

/// <summary>BR-BOM-07: how many units of the component's base unit make one unit of the line's unit.</summary>
public static class UomRules
{
    /// <summary>
    /// Null when the two units cannot be converted. Order: the same unit (1); the product's own conversion in either direction;
    /// units that share a base unit in MasterData (<c>factor(from) / factor(to)</c>).
    /// </summary>
    public static decimal? Factor(UnitFacts from, UnitFacts to, IEnumerable<ProductConversionFacts> productConversions)
    {
        if (from.Id == to.Id) return 1m;

        var own = productConversions.ToList();
        var direct = own.FirstOrDefault(c => c.FromUomId == from.Id && c.ToUomId == to.Id && c.Factor > 0);
        if (direct is not null) return direct.Factor;
        var reverse = own.FirstOrDefault(c => c.FromUomId == to.Id && c.ToUomId == from.Id && c.Factor > 0);
        if (reverse is not null) return 1m / reverse.Factor;

        if ((from.BaseUnitId ?? from.Id) != (to.BaseUnitId ?? to.Id)) return null;
        var factorTo = to.BaseUnitId is null ? 1m : to.ConversionFactor;
        if (factorTo <= 0) return null;
        var factorFrom = from.BaseUnitId is null ? 1m : from.ConversionFactor;
        return factorFrom / factorTo;
    }
}

/// <summary>What changed between two versions of a BOM (T2.4.1).</summary>
public sealed record BomDifference(string Kind, Guid ComponentItemId, string ComponentCode, int? OperationSeq, decimal? LeftQuantity, decimal? RightQuantity, string? Detail);

public static class BomComparer
{
    /// <summary>Lines are matched by component and operation. Added = only on the right, Removed = only on the left, Changed = quantity, scrap, unit or flags differ.</summary>
    public static IReadOnlyList<BomDifference> Compare(Bom left, Bom right)
    {
        var l = left.Lines.GroupBy(x => (x.ComponentItemId, x.OperationSeq, x.LineType, x.AlternativeGroup)).ToDictionary(g => g.Key, g => g.First());
        var r = right.Lines.GroupBy(x => (x.ComponentItemId, x.OperationSeq, x.LineType, x.AlternativeGroup)).ToDictionary(g => g.Key, g => g.First());
        var result = new List<BomDifference>();

        foreach (var (key, a) in l)
        {
            if (!r.TryGetValue(key, out var b))
            {
                result.Add(new BomDifference("Removed", a.ComponentItemId, a.ComponentCode, a.OperationSeq, a.Quantity, null, null));
                continue;
            }
            var changes = new List<string>();
            if (a.Quantity != b.Quantity) changes.Add($"quantity {a.Quantity:0.######} -> {b.Quantity:0.######}");
            if (a.UomId != b.UomId) changes.Add("unit");
            if (a.ScrapPct != b.ScrapPct) changes.Add($"scrap {a.ScrapPct} -> {b.ScrapPct}");
            if (a.IsFixedQuantity != b.IsFixedQuantity) changes.Add("fixed quantity");
            if (a.IsPhantom != b.IsPhantom) changes.Add("phantom");
            if (a.IssueMethod != b.IssueMethod) changes.Add("issue method");
            if (a.AlternativePriority != b.AlternativePriority) changes.Add("alternative priority");
            if (a.CostAllocationPct != b.CostAllocationPct) changes.Add("cost allocation");
            if (changes.Count > 0)
                result.Add(new BomDifference("Changed", a.ComponentItemId, a.ComponentCode, a.OperationSeq, a.Quantity, b.Quantity, string.Join("; ", changes)));
        }

        foreach (var (key, b) in r)
            if (!l.ContainsKey(key))
                result.Add(new BomDifference("Added", b.ComponentItemId, b.ComponentCode, b.OperationSeq, null, b.Quantity, null));

        return result.OrderBy(d => d.ComponentCode).ThenBy(d => d.Kind).ToList();
    }
}
