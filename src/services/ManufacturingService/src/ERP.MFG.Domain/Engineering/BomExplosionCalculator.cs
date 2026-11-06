using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Domain.Engineering;

/// <summary>A material the order needs, in the component's base unit (BR-BOM-06).</summary>
public sealed record RequirementLine(
    Guid ComponentItemId, string ComponentCode, string ComponentName, decimal Quantity, int? OperationSeq,
    IssueMethod IssueMethod, string? AlternativeGroup, int SourceLineNo);

/// <summary>A by-product or co-product the order yields.</summary>
public sealed record OutputLine(Guid ItemId, string ItemCode, string ItemName, BomLineType LineType, decimal PlannedQuantity, decimal? CostAllocationPct, int SourceLineNo);

public sealed record ExplosionResult(Guid BomId, Guid ItemId, int Version, decimal OrderQuantity, IReadOnlyList<RequirementLine> Requirements, IReadOnlyList<OutputLine> Outputs);

/// <summary>One node of the indented (multi-level) explosion.</summary>
public sealed class IndentedNode
{
    public int Level { get; init; }
    public Guid ItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public bool IsPhantom { get; init; }
    public bool IsSubstitute { get; init; }
    public string? AlternativeGroup { get; init; }
    public int? OperationSeq { get; init; }
    public int? BomVersion { get; init; }
    public List<IndentedNode> Children { get; } = [];
}

/// <summary>
/// The material requirement arithmetic of a BOM (BA spec 01 BR-BOM-06, -08, -10, -11), pure: the unit factors and the decimals of each component are
/// already in the snapshot, and the BOM of every phantom / sub-assembly is looked up in <c>resolve</c>.
/// </summary>
public static class BomExplosionCalculator
{
    /// <summary>
    /// <c>Req = OrderQty / BaseQuantity x Quantity x (1 + Scrap%) / Yield%</c>, or <c>Quantity x (1 + Scrap%)</c> for a fixed quantity,
    /// multiplied by the unit factor and rounded UP to the decimals of the component's base unit. The division is done last so a
    /// result that is exact on paper is exact here.
    /// </summary>
    public static decimal Required(BomSnapshot bom, BomLineSnapshot line, decimal orderQuantity, bool round = true)
    {
        decimal value;
        if (line.IsFixedQuantity)
        {
            value = line.Quantity * (100m + line.ScrapPct) * line.FactorToBase / 100m;
        }
        else
        {
            value = orderQuantity * line.Quantity * (100m + line.ScrapPct) * line.FactorToBase
                    / (bom.BaseQuantity * bom.YieldPct);
        }
        return round ? RoundUp(value, line.BaseDecimals) : value;
    }

    public static decimal RoundUp(decimal value, int decimals)
    {
        var scale = (decimal)Math.Pow(10, decimals);
        return Math.Ceiling(value * scale) / scale;
    }

    /// <summary>
    /// One level of the order's materials: phantoms are replaced by their own components (BR-BOM-08), only the priority-1 line of an alternative
    /// group is counted (BR-BOM-11), by-products and co-products are listed as outputs without scrap. A phantom without an effective BOM
    /// is <c>MFG_BOM_011</c>; going deeper than <paramref name="maxLevels"/> is <c>MFG_BOM_008</c>.
    /// </summary>
    public static ExplosionResult Explode(BomSnapshot bom, decimal orderQuantity, Func<Guid, BomSnapshot?> resolve, int maxLevels)
    {
        MfgRules.ThrowIf(orderQuantity <= 0, MfgErrors.Po001);
        var requirements = new List<RequirementLine>();
        var outputs = new List<OutputLine>();
        Walk(bom, orderQuantity, 1, null, resolve, maxLevels, requirements, outputs);
        return new ExplosionResult(bom.BomId, bom.ItemId, bom.Version, orderQuantity,
            Merge(requirements), outputs);
    }

    private static void Walk(BomSnapshot bom, decimal quantity, int level, int? inheritedOperation, Func<Guid, BomSnapshot?> resolve, int maxLevels,
        List<RequirementLine> requirements, List<OutputLine> outputs)
    {
        MfgRules.ThrowIf(level > maxLevels, MfgErrors.Bom008, ("max", maxLevels));

        foreach (var line in bom.Lines.OrderBy(l => l.LineNo))
        {
            if (line.LineType != BomLineType.Component)
            {
                if (level == 1) // outputs of a phantom are not the order's outputs
                    outputs.Add(new OutputLine(line.ComponentItemId, line.ComponentCode, line.ComponentName, line.LineType,
                        RoundUp(quantity / bom.BaseQuantity * line.Quantity * line.FactorToBase, line.BaseDecimals), line.CostAllocationPct, line.LineNo));
                continue;
            }

            if (line.AlternativeGroup is not null && line.AlternativePriority != 1) continue; // a substitute is used only when chosen (BR-MAT-09)

            if (line.IsPhantom)
            {
                var phantom = resolve(line.ComponentItemId) ?? throw MfgRules.Fail(MfgErrors.Bom011, ("item", line.ComponentCode));
                var needed = Required(bom, line, quantity, round: false); // a phantom is never stocked: no rounding between the levels
                Walk(phantom, needed, level + 1, line.OperationSeq ?? inheritedOperation, resolve, maxLevels, requirements, outputs);
                continue;
            }

            requirements.Add(new RequirementLine(line.ComponentItemId, line.ComponentCode, line.ComponentName, Required(bom, line, quantity),
                line.OperationSeq ?? inheritedOperation, line.IssueMethod, line.AlternativeGroup, line.LineNo));
        }
    }

    /// <summary>The same component at the same operation from two places (a phantom and the BOM) is one requirement line.</summary>
    private static IReadOnlyList<RequirementLine> Merge(List<RequirementLine> lines)
        => lines.GroupBy(l => (l.ComponentItemId, l.OperationSeq, l.IssueMethod))
            .Select(g => g.Count() == 1 ? g.First() : g.First() with { Quantity = g.Sum(x => x.Quantity) })
            .ToList();

    /// <summary>
    /// The indented explosion for lookup (S08): every level, a made component expanded with its own effective BOM for the quantity of it that
    /// is needed (rounded up to whole base-unit decimals); a bought component or one without a BOM is a leaf. Substitutes are shown, not expanded.
    /// </summary>
    public static IndentedNode ExplodeMultiLevel(BomSnapshot bom, decimal orderQuantity, Func<Guid, BomSnapshot?> resolve, int maxLevels)
    {
        MfgRules.ThrowIf(orderQuantity <= 0, MfgErrors.Po001);
        var root = new IndentedNode { Level = 0, ItemId = bom.ItemId, ItemCode = bom.ItemCode, ItemName = bom.ItemName, Quantity = orderQuantity, BomVersion = bom.Version };
        Expand(root, bom, orderQuantity, 1, resolve, maxLevels);
        return root;
    }

    private static void Expand(IndentedNode parent, BomSnapshot bom, decimal quantity, int level, Func<Guid, BomSnapshot?> resolve, int maxLevels)
    {
        MfgRules.ThrowIf(level > maxLevels, MfgErrors.Bom008, ("max", maxLevels));

        foreach (var line in bom.Lines.Where(l => l.LineType == BomLineType.Component).OrderBy(l => l.LineNo))
        {
            var substitute = line.AlternativeGroup is not null && line.AlternativePriority != 1;
            var needed = Required(bom, line, quantity, round: !line.IsPhantom);
            var child = new IndentedNode
            {
                Level = level, ItemId = line.ComponentItemId, ItemCode = line.ComponentCode, ItemName = line.ComponentName, Quantity = needed,
                IsPhantom = line.IsPhantom, IsSubstitute = substitute, AlternativeGroup = line.AlternativeGroup, OperationSeq = line.OperationSeq,
            };
            parent.Children.Add(child);
            if (substitute) continue;

            var sub = resolve(line.ComponentItemId);
            if (sub is null)
            {
                MfgRules.ThrowIf(line.IsPhantom, MfgErrors.Bom011, ("item", line.ComponentCode));
                continue;
            }
            Expand(child, sub, needed, level + 1, resolve, maxLevels);
        }
    }
}
