using ERP.MFG.Domain.Common;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>The editable values of one BOM line (BA spec 01 §5.8, §7.2). The component's code and name are a snapshot for display.</summary>
public sealed record BomLineValues(
    BomLineType LineType,
    Guid ComponentItemId,
    string ComponentCode,
    string ComponentName,
    decimal Quantity,
    Guid UomId,
    decimal ScrapPct,
    bool IsFixedQuantity,
    bool IsPhantom,
    IssueMethod IssueMethod,
    int? OperationSeq,
    string? AlternativeGroup,
    int? AlternativePriority,
    decimal? CostAllocationPct,
    string? Notes);

/// <summary>One component, by-product or co-product of a bill of materials.</summary>
public class BomLine : BaseEntity
{
    private BomLine() { }

    public Guid BomId { get; private set; }
    public int LineNo { get; private set; }
    public BomLineType LineType { get; private set; } = BomLineType.Component;
    public Guid ComponentItemId { get; private set; }
    public string ComponentCode { get; private set; } = string.Empty;
    public string ComponentName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public Guid UomId { get; private set; }
    public decimal ScrapPct { get; private set; }
    public bool IsFixedQuantity { get; private set; }
    public bool IsPhantom { get; private set; }
    public IssueMethod IssueMethod { get; private set; } = IssueMethod.Manual;
    public int? OperationSeq { get; private set; }
    public string? AlternativeGroup { get; private set; }
    public int? AlternativePriority { get; private set; }
    public decimal? CostAllocationPct { get; private set; }
    public string? Notes { get; private set; }

    internal static BomLine Create(Guid bomId, int lineNo, BomLineValues values)
    {
        var line = new BomLine { Id = Guid.NewGuid(), BomId = bomId, LineNo = lineNo };
        line.Apply(values);
        return line;
    }

    internal void Apply(BomLineValues v)
    {
        LineType = v.LineType;
        ComponentItemId = v.ComponentItemId;
        ComponentCode = v.ComponentCode;
        ComponentName = v.ComponentName;
        Quantity = v.Quantity;
        UomId = v.UomId;
        ScrapPct = v.ScrapPct;
        IsFixedQuantity = v.IsFixedQuantity;
        IsPhantom = v.IsPhantom;
        IssueMethod = v.IssueMethod;
        OperationSeq = v.OperationSeq;
        AlternativeGroup = v.AlternativeGroup;
        AlternativePriority = v.AlternativePriority;
        CostAllocationPct = v.CostAllocationPct;
        Notes = v.Notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public BomLineValues ToValues() => new(LineType, ComponentItemId, ComponentCode, ComponentName, Quantity, UomId, ScrapPct, IsFixedQuantity,
        IsPhantom, IssueMethod, OperationSeq, AlternativeGroup, AlternativePriority, CostAllocationPct, Notes);

    internal void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }
}

/// <summary>
/// A bill of materials: what one <see cref="BaseQuantity"/> of the item is made of, in one version (BR-BOM-02). Draft is editable;
/// Approved and Obsolete never change (a change is a new version made by <see cref="CopyAsNewDraft"/>), except the end of an obsolete version.
/// </summary>
public class Bom : BaseEntity, ERP.MFG.Domain.Engineering.IVersionedDocument
{
    private readonly List<BomLine> _allLines = [];

    private Bom() { }

    public Guid ItemId { get; private set; }
    public Guid PlantId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public decimal BaseQuantity { get; private set; } = 1m;
    public decimal YieldPct { get; private set; } = 100m;
    public VersionStatus Status { get; private set; } = VersionStatus.Draft;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    public IReadOnlyCollection<BomLine> Lines => _allLines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNo).ToList();

    /// <summary>Persistence access to the loaded children (EF fills the field).</summary>
    public IReadOnlyList<BomLine> AllLines => _allLines;

    public static Bom Create(Guid itemId, Guid plantId, string itemCode, string itemName, int version, decimal baseQuantity, decimal yieldPct,
        DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        var bom = new Bom { Id = Guid.NewGuid(), ItemId = itemId, PlantId = plantId, ItemCode = itemCode, ItemName = itemName, Version = version };
        bom.SetHeader(baseQuantity, yieldPct, effectiveFrom, effectiveTo, description);
        return bom;
    }

    /// <summary>Only a Draft can be changed (BR-BOM-02, <c>MFG_BOM_002</c>).</summary>
    public void EnsureDraft() => MfgRules.ThrowIf(Status != VersionStatus.Draft, MfgErrors.Bom002);

    public void UpdateHeader(decimal baseQuantity, decimal yieldPct, DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        EnsureDraft();
        SetHeader(baseQuantity, yieldPct, effectiveFrom, effectiveTo, description);
    }

    private void SetHeader(decimal baseQuantity, decimal yieldPct, DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        MfgRules.ThrowIf(!HasAtMostDecimals(baseQuantity, 6) || baseQuantity <= 0 || yieldPct <= 0 || yieldPct > 100, MfgErrors.Bom005);
        if (effectiveTo is { } to && to < effectiveFrom) throw MfgRules.Invalid("The end of the validity period cannot be before its start.");
        BaseQuantity = baseQuantity;
        YieldPct = yieldPct;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Lines ───────────────────────────────────────────────────────────────────────────────────────────────

    public BomLine AddLine(BomLineValues values)
    {
        EnsureDraft();
        var lineNo = (AllLines.Count == 0 ? 0 : AllLines.Max(l => l.LineNo)) / 10 * 10 + 10;
        CheckLine(values, exceptLineNo: null);
        var line = BomLine.Create(Id, lineNo, values);
        _allLines.Add(line);
        UpdatedAt = DateTime.UtcNow;
        return line;
    }

    public BomLine UpdateLine(int lineNo, BomLineValues values)
    {
        EnsureDraft();
        var line = FindLine(lineNo);
        CheckLine(values, exceptLineNo: lineNo);
        line.Apply(values);
        UpdatedAt = DateTime.UtcNow;
        return line;
    }

    public void RemoveLine(int lineNo)
    {
        EnsureDraft();
        FindLine(lineNo).MarkDeleted();
        UpdatedAt = DateTime.UtcNow;
    }

    private BomLine FindLine(int lineNo)
        => Lines.FirstOrDefault(l => l.LineNo == lineNo) ?? throw MfgRules.Invalid($"BOM line {lineNo} does not exist.");

    /// <summary>The checks of one line against the rest of the BOM: quantities (005), self reference (004), duplicates (010), co-product allocation (007).</summary>
    private void CheckLine(BomLineValues v, int? exceptLineNo)
    {
        if (!Enum.IsDefined(v.LineType) || !Enum.IsDefined(v.IssueMethod)) throw MfgRules.Fail(MfgErrors.Bom005);
        if (v.Quantity <= 0 || !HasAtMostDecimals(v.Quantity, 6) || v.ScrapPct < 0 || v.ScrapPct > 99.99m || !HasAtMostDecimals(v.ScrapPct, 2))
            throw MfgRules.Fail(MfgErrors.Bom005);
        if (v.UomId == Guid.Empty) throw MfgRules.Invalid("A unit of measure is required.");
        if (v.OperationSeq is <= 0) throw MfgRules.Invalid("The operation sequence must be greater than 0.");

        if (v.ComponentItemId == ItemId) throw MfgRules.Fail(MfgErrors.Bom004, ("path", $"{ItemCode} > {ItemCode}")); // BR-BOM-04: directly its own component

        var group = string.IsNullOrWhiteSpace(v.AlternativeGroup) ? null : v.AlternativeGroup.Trim().ToUpperInvariant();
        if (group is { Length: > 20 } || (group is null && v.AlternativePriority is not null) || (group is not null && v.AlternativePriority is null or < 1))
            throw MfgRules.Invalid("An alternative group needs a priority of 1 or more, and a priority needs a group (group up to 20 characters).");
        if (v.LineType != BomLineType.Component && (group is not null || v.IsPhantom))
            throw MfgRules.Invalid("Only a component can be phantom or belong to an alternative group.");

        var others = Lines.Where(l => l.LineNo != exceptLineNo).ToList();

        // BR-BOM-12: the same component twice at the same operation, unless one of them is an alternative of the other.
        if (group is null && v.LineType == BomLineType.Component
            && others.Any(l => l.LineType == BomLineType.Component && l.AlternativeGroup is null && l.ComponentItemId == v.ComponentItemId && l.OperationSeq == v.OperationSeq))
            throw MfgRules.Fail(MfgErrors.Bom010, ("item", v.ComponentCode), ("seq", v.OperationSeq?.ToString() ?? "-"));

        if (v.LineType == BomLineType.CoProduct)
        {
            if (v.CostAllocationPct is null or < 0 or >= 100) throw MfgRules.Fail(MfgErrors.Bom007);
            var sum = others.Where(l => l.LineType == BomLineType.CoProduct).Sum(l => l.CostAllocationPct ?? 0m) + v.CostAllocationPct.Value;
            if (sum >= 100m) throw MfgRules.Fail(MfgErrors.Bom007);
        }
        else if (v.CostAllocationPct is not null)
        {
            throw MfgRules.Invalid("Only a co-product takes a share of the cost.");
        }
    }

    /// <summary>
    /// The checks that need the whole BOM, run before approval: each alternative group has exactly one priority-1 line (BR-BOM-11, <c>MFG_BOM_009</c>)
    /// and there is something to make it from.
    /// </summary>
    public void ValidateForApproval()
    {
        foreach (var group in Lines.Where(l => l.AlternativeGroup is not null).GroupBy(l => l.AlternativeGroup!))
            MfgRules.ThrowIf(group.Count(l => l.AlternativePriority == 1) != 1, MfgErrors.Bom009, ("group", group.Key));
        if (!Lines.Any(l => l.LineType == BomLineType.Component)) throw MfgRules.Invalid("A BOM needs at least one component.");
    }

    // ── Life cycle ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Draft -> Approved (BR-BOM-02). The caller has already planned the overlap with other versions (<see cref="ERP.MFG.Domain.Engineering.DocumentVersioning"/>).
    /// </summary>
    public void Approve(string approvedBy, DateTime approvedAt)
    {
        EnsureDraft();
        ValidateForApproval();
        Status = VersionStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Approved -> Obsolete, ending on <paramref name="effectiveTo"/> (never after it ended earlier).</summary>
    public void Obsolete(DateOnly effectiveTo)
    {
        MfgRules.ThrowIf(Status != VersionStatus.Approved, MfgErrors.Bom002);
        if (effectiveTo < EffectiveFrom) throw MfgRules.Invalid("The end of the validity period cannot be before its start.");
        Status = VersionStatus.Obsolete;
        EffectiveTo = EffectiveTo is { } current && current < effectiveTo ? current : effectiveTo;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>The one change an obsolete version allows: when it ends (BR-BOM-02).</summary>
    public void SetEffectiveToOfObsolete(DateOnly effectiveTo)
    {
        MfgRules.ThrowIf(Status != VersionStatus.Obsolete, MfgErrors.Bom002);
        if (effectiveTo < EffectiveFrom) throw MfgRules.Invalid("The end of the validity period cannot be before its start.");
        EffectiveTo = effectiveTo;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Used when a newer version starts: this approved version ends the day before (BR-BOM-03).</summary>
    public void CloseOn(DateOnly effectiveTo)
    {
        EffectiveTo = effectiveTo;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>BR-BOM-15: a new Draft, <paramref name="newVersion"/>, with every line copied and the given start date.</summary>
    public Bom CopyAsNewDraft(int newVersion, DateOnly effectiveFrom)
    {
        var copy = Create(ItemId, PlantId, ItemCode, ItemName, newVersion, BaseQuantity, YieldPct, effectiveFrom, null, Description);
        foreach (var line in Lines) copy._allLines.Add(BomLine.Create(copy.Id, line.LineNo, line.ToValues()));
        return copy;
    }

    /// <summary>A Draft can be deleted; anything that was ever approved is kept (<c>MFG_BOM_002</c>).</summary>
    public void MarkDeleted()
    {
        EnsureDraft();
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Is this the BOM that applies on <paramref name="date"/> (BR-BOM-03)? Only an approved version counts.</summary>
    public bool IsEffectiveOn(DateOnly date) => Status == VersionStatus.Approved && EffectiveFrom <= date && (EffectiveTo is null || date <= EffectiveTo);

    private static bool HasAtMostDecimals(decimal value, int decimals) => decimal.Round(value, decimals) == value;
}
