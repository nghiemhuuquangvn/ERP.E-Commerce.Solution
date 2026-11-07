using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Domain.Engineering;

/// <summary>One line of a BOM as the calculators see it: the unit factor and the decimals of the component's base unit are already resolved.</summary>
public sealed record BomLineSnapshot(
    int LineNo,
    BomLineType LineType,
    Guid ComponentItemId,
    string ComponentCode,
    string ComponentName,
    decimal Quantity,
    Guid UomId,
    decimal FactorToBase,
    int BaseDecimals,
    decimal ScrapPct,
    bool IsFixedQuantity,
    bool IsPhantom,
    IssueMethod IssueMethod,
    int? OperationSeq,
    string? AlternativeGroup,
    int? AlternativePriority,
    decimal? CostAllocationPct);

/// <summary>A BOM version as the calculators see it.</summary>
public sealed record BomSnapshot(
    Guid BomId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    int Version,
    decimal BaseQuantity,
    decimal YieldPct,
    IReadOnlyList<BomLineSnapshot> Lines);

/// <summary>Graph questions over the approved BOMs: cycles (BR-BOM-04), low-level codes (BR-BOM-13) and where an item is used.</summary>
public static class BomGraph
{
    /// <summary>parent item -> the items it consumes (components only; by-products and co-products are outputs, not inputs).</summary>
    public static Dictionary<Guid, HashSet<Guid>> EdgesOf(IEnumerable<Bom> boms)
    {
        var edges = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var bom in boms)
        {
            if (!edges.TryGetValue(bom.ItemId, out var children)) edges[bom.ItemId] = children = [];
            foreach (var line in bom.Lines.Where(l => l.LineType == BomLineType.Component)) children.Add(line.ComponentItemId);
        }
        return edges;
    }

    /// <summary>
    /// The first cycle reachable from <paramref name="start"/> by depth-first search, as the item path that closes it
    /// (<c>A, B, A</c>); null when there is none. The edges must already contain the BOM that is being approved.
    /// </summary>
    public static IReadOnlyList<Guid>? FindCycle(IReadOnlyDictionary<Guid, HashSet<Guid>> edges, Guid start)
    {
        var path = new List<Guid>();
        var onPath = new HashSet<Guid>();
        var done = new HashSet<Guid>();

        IReadOnlyList<Guid>? Visit(Guid node)
        {
            if (onPath.Contains(node))
            {
                var from = path.IndexOf(node);
                return path.Skip(from).Append(node).ToList();
            }
            if (!done.Add(node)) return null;

            onPath.Add(node);
            path.Add(node);
            if (edges.TryGetValue(node, out var children))
                foreach (var child in children.OrderBy(c => c))
                {
                    var cycle = Visit(child);
                    if (cycle is not null) return cycle;
                }
            path.RemoveAt(path.Count - 1);
            onPath.Remove(node);
            return null;
        }

        return Visit(start);
    }

    /// <summary>
    /// BR-BOM-13: the low-level code of every item that appears in the edges = the deepest level it has in any BOM
    /// (an item that is only a parent is level 0). The edges must be acyclic.
    /// </summary>
    public static Dictionary<Guid, int> LowLevelCodes(IReadOnlyDictionary<Guid, HashSet<Guid>> edges)
    {
        var all = edges.Keys.Concat(edges.Values.SelectMany(v => v)).Distinct().ToList();
        var parents = all.ToDictionary(i => i, _ => 0);
        foreach (var child in edges.Values.SelectMany(v => v)) parents[child]++;

        var level = all.ToDictionary(i => i, _ => 0);
        var ready = new Queue<Guid>(all.Where(i => parents[i] == 0));
        var visited = 0;
        while (ready.Count > 0)
        {
            var node = ready.Dequeue();
            visited++;
            if (!edges.TryGetValue(node, out var children)) continue;
            foreach (var child in children)
            {
                level[child] = Math.Max(level[child], level[node] + 1);
                if (--parents[child] == 0) ready.Enqueue(child);
            }
        }

        // A cycle would leave items unvisited; the approval flow refuses cycles before it asks for codes.
        MfgRules.ThrowIf(visited != all.Count, MfgErrors.Bom004, ("path", "?"));
        return level;
    }

    public sealed record WhereUsedRow(int Level, Guid ParentItemId, string ParentCode, string ParentName, int BomVersion, Guid ComponentItemId, decimal QuantityPer, Guid UomId);

    /// <summary>Everything that consumes an item, directly (level 1) and through its parents (level 2 ...), over the approved BOMs.</summary>
    public static IReadOnlyList<WhereUsedRow> WhereUsed(IEnumerable<Bom> approved, Guid itemId, int maxLevels)
    {
        var boms = approved.ToList();
        var rows = new List<WhereUsedRow>();
        var seen = new HashSet<Guid>(); // a parent is listed once, at the lowest level it uses the item
        var frontier = new List<Guid> { itemId };

        for (var level = 1; level <= maxLevels && frontier.Count > 0; level++)
        {
            var next = new List<Guid>();
            foreach (var target in frontier)
                foreach (var bom in boms)
                    foreach (var line in bom.Lines.Where(l => l.LineType == BomLineType.Component && l.ComponentItemId == target))
                        if (seen.Add(bom.Id))
                        {
                            rows.Add(new WhereUsedRow(level, bom.ItemId, bom.ItemCode, bom.ItemName, bom.Version, target, line.Quantity, line.UomId));
                            next.Add(bom.ItemId);
                        }
            frontier = next.Distinct().ToList();
        }
        return rows;
    }
}
