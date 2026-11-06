using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Domain.Engineering;

/// <summary>BR-BOM-03: versions of one item in one plant must not overlap in time.</summary>
public static class BomVersioning
{
    /// <summary>
    /// Plans the approval of <paramref name="candidate"/> against the other approved versions of the same item and plant.
    /// An open-ended version that started before the candidate ends the day before the candidate starts (it is returned to be closed);
    /// any other overlap is refused with <c>MFG_BOM_003</c> naming the version it clashes with.
    /// </summary>
    public static IReadOnlyList<(Bom Bom, DateOnly NewEffectiveTo)> PlanApproval(Bom candidate, IEnumerable<Bom> approvedSiblings)
    {
        var toClose = new List<(Bom, DateOnly)>();
        foreach (var other in approvedSiblings.Where(b => b.Id != candidate.Id && b.Status == VersionStatus.Approved && b.ItemId == candidate.ItemId && b.PlantId == candidate.PlantId))
        {
            if (!Overlaps(candidate.EffectiveFrom, candidate.EffectiveTo, other.EffectiveFrom, other.EffectiveTo)) continue;

            if (other.EffectiveTo is null && other.EffectiveFrom < candidate.EffectiveFrom)
                toClose.Add((other, candidate.EffectiveFrom.AddDays(-1)));
            else
                throw MfgRules.Fail(MfgErrors.Bom003, ("version", other.Version));
        }
        return toClose;
    }

    /// <summary>Applies a plan returned by <see cref="PlanApproval"/>.</summary>
    public static void Apply(IEnumerable<(Bom Bom, DateOnly NewEffectiveTo)> plan)
    {
        foreach (var (bom, to) in plan) bom.CloseOn(to);
    }

    /// <summary>The version that applies on a date: the approved one whose period contains it (BR-BOM-03).</summary>
    public static Bom? Resolve(IEnumerable<Bom> versions, DateOnly date) => versions.Where(b => b.IsEffectiveOn(date)).OrderByDescending(b => b.Version).FirstOrDefault();

    private static bool Overlaps(DateOnly fromA, DateOnly? toA, DateOnly fromB, DateOnly? toB)
        => fromA <= (toB ?? DateOnly.MaxValue) && fromB <= (toA ?? DateOnly.MaxValue);
}
