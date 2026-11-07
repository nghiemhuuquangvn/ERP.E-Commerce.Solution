using ERP.MFG.Domain.Common;

namespace ERP.MFG.Domain.Engineering;

/// <summary>A document that exists in numbered versions with a validity period per item and plant: a BOM or a routing (BR-BOM-02/03, BR-RT-03).</summary>
public interface IVersionedDocument
{
    Guid Id { get; }
    Guid ItemId { get; }
    Guid PlantId { get; }
    int Version { get; }
    VersionStatus Status { get; }
    DateOnly EffectiveFrom { get; }
    DateOnly? EffectiveTo { get; }
    bool IsEffectiveOn(DateOnly date);

    /// <summary>Used when a newer version starts: this approved version ends on the given day.</summary>
    void CloseOn(DateOnly effectiveTo);
}

/// <summary>BR-BOM-03 / BR-RT-03: versions of one item in one plant must not overlap in time.</summary>
public static class DocumentVersioning
{
    /// <summary>
    /// Plans the approval of <paramref name="candidate"/> against the other approved versions of the same item and plant.
    /// An open-ended version that started before the candidate ends the day before the candidate starts (it is returned to be closed);
    /// any other overlap is refused with <c>MFG_BOM_003</c> naming the version it clashes with.
    /// </summary>
    public static IReadOnlyList<(T Document, DateOnly NewEffectiveTo)> PlanApproval<T>(T candidate, IEnumerable<T> approvedSiblings) where T : IVersionedDocument
    {
        var toClose = new List<(T, DateOnly)>();
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

    /// <summary>Applies a plan returned by <see cref="PlanApproval{T}"/>.</summary>
    public static void Apply<T>(IEnumerable<(T Document, DateOnly NewEffectiveTo)> plan) where T : IVersionedDocument
    {
        foreach (var (document, to) in plan) document.CloseOn(to);
    }

    /// <summary>The version that applies on a date: the approved one whose period contains it.</summary>
    public static T? Resolve<T>(IEnumerable<T> versions, DateOnly date) where T : class, IVersionedDocument
        => versions.Where(b => b.IsEffectiveOn(date)).OrderByDescending(b => b.Version).FirstOrDefault();

    private static bool Overlaps(DateOnly fromA, DateOnly? toA, DateOnly fromB, DateOnly? toB)
        => fromA <= (toB ?? DateOnly.MaxValue) && fromB <= (toA ?? DateOnly.MaxValue);
}
