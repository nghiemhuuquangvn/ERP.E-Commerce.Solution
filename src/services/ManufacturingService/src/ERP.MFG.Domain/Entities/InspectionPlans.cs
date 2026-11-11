using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>The editable values of one characteristic of an inspection plan (BA spec 01 §5.15 BR-QC-01, §7.4).</summary>
public sealed record InspectionCharacteristicValues(int Seq, string Name, CharacteristicKind Kind, decimal? MinValue, decimal? MaxValue, string? Unit, bool IsCritical);

/// <summary>One thing that is checked: a measured value that must lie in [Min, Max], or an attribute that passes or fails. A critical one must always pass.</summary>
public class InspectionCharacteristic : BaseEntity
{
    private InspectionCharacteristic() { }

    public Guid PlanId { get; private set; }
    public int Seq { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public CharacteristicKind Kind { get; private set; } = CharacteristicKind.Measurement;
    public decimal? MinValue { get; private set; }
    public decimal? MaxValue { get; private set; }
    public string? Unit { get; private set; }
    public bool IsCritical { get; private set; }

    internal static InspectionCharacteristic Create(Guid planId, InspectionCharacteristicValues values)
    {
        var c = new InspectionCharacteristic { Id = Guid.NewGuid(), PlanId = planId };
        c.Apply(values);
        return c;
    }

    internal void Apply(InspectionCharacteristicValues v)
    {
        Seq = v.Seq;
        Name = v.Name;
        Kind = v.Kind;
        MinValue = v.MinValue;
        MaxValue = v.MaxValue;
        Unit = v.Unit;
        IsCritical = v.IsCritical;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    public InspectionCharacteristicValues ToValues() => new(Seq, Name, Kind, MinValue, MaxValue, Unit, IsCritical);

    /// <summary>Does a measured value lie in the range? A bound that is not set does not limit.</summary>
    public bool InRange(decimal value) => (MinValue is null || value >= MinValue) && (MaxValue is null || value <= MaxValue);
}

/// <summary>
/// An inspection plan (BR-QC-01): what to check, how many units to take and how many minor defects are tolerated. A plan is a <b>series</b> (plant + <see cref="Code"/>)
/// that exists in numbered versions (Draft -> Approved -> Obsolete, approved versions never change, a change is a new version made by <see cref="CopyAsNewDraft"/>);
/// code, type and item belong to the series. A profile or an operation points at the series (any of its versions); the version in force on the day is used.
/// </summary>
public class InspectionPlan : BaseEntity, IVersionedDocument
{
    private readonly List<InspectionCharacteristic> _allCharacteristics = [];

    private InspectionPlan() { }

    public string Code { get; private set; } = string.Empty;
    public InspectionType Type { get; private set; }
    public Guid PlantId { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public VersionStatus Status { get; private set; } = VersionStatus.Draft;
    public SamplingMethod SamplingMethod { get; private set; } = SamplingMethod.Fixed;
    public decimal? SampleValue { get; private set; }
    public int AllowedMinorDefects { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    public IReadOnlyCollection<InspectionCharacteristic> Characteristics => _allCharacteristics.Where(c => !c.IsDeleted).OrderBy(c => c.Seq).ToList();

    /// <summary>Persistence access to the loaded children (EF fills the field).</summary>
    public IReadOnlyList<InspectionCharacteristic> AllCharacteristics => _allCharacteristics;

    public static InspectionPlan Create(Guid plantId, Guid itemId, string itemCode, string itemName, string code, InspectionType type, int version,
        SamplingMethod sampling, decimal? sampleValue, int allowedMinorDefects, DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        if (!Enum.IsDefined(type)) throw MfgRules.Invalid("The type of an inspection plan is IPQC or FQC.");
        var plan = new InspectionPlan
        {
            Id = Guid.NewGuid(), PlantId = plantId, ItemId = itemId, ItemCode = itemCode, ItemName = itemName, Version = version, Type = type,
            Code = MasterCode.Valid(code),
        };
        plan.SetHeader(sampling, sampleValue, allowedMinorDefects, effectiveFrom, effectiveTo, description);
        return plan;
    }

    public void EnsureDraft() => MfgRules.ThrowIf(Status != VersionStatus.Draft, MfgErrors.Bom002);

    public void UpdateHeader(SamplingMethod sampling, decimal? sampleValue, int allowedMinorDefects, DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        EnsureDraft();
        SetHeader(sampling, sampleValue, allowedMinorDefects, effectiveFrom, effectiveTo, description);
    }

    private void SetHeader(SamplingMethod sampling, decimal? sampleValue, int allowedMinorDefects, DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        if (!Enum.IsDefined(sampling)) throw MfgRules.Invalid("The sampling method is Fixed, Percent or Full.");
        if (sampling == SamplingMethod.Fixed && sampleValue is not > 0) throw MfgRules.Invalid("A fixed sample needs a number of units greater than 0.");
        if (sampling == SamplingMethod.Percent && sampleValue is not (> 0 and <= 100)) throw MfgRules.Invalid("A percentage sample needs a value above 0 and up to 100.");
        if (allowedMinorDefects < 0) throw MfgRules.Invalid("The number of tolerated minor defects cannot be negative.");
        if (effectiveTo is { } to && to < effectiveFrom) throw MfgRules.Invalid("The end of the validity period cannot be before its start.");

        SamplingMethod = sampling;
        SampleValue = sampling == SamplingMethod.Full ? null : sampleValue;
        AllowedMinorDefects = allowedMinorDefects;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Characteristics ─────────────────────────────────────────────────────────────────────────────────────

    public InspectionCharacteristic AddCharacteristic(InspectionCharacteristicValues values)
    {
        EnsureDraft();
        Check(values, exceptSeq: null);
        var c = InspectionCharacteristic.Create(Id, values);
        _allCharacteristics.Add(c);
        UpdatedAt = DateTime.UtcNow;
        return c;
    }

    /// <summary>The next free sequence number in steps of 10.</summary>
    public int NextSeq() => (Characteristics.Count == 0 ? 0 : Characteristics.Max(c => c.Seq)) / 10 * 10 + 10;

    public InspectionCharacteristic UpdateCharacteristic(int seq, InspectionCharacteristicValues values)
    {
        EnsureDraft();
        var c = Find(seq);
        Check(values, exceptSeq: seq);
        c.Apply(values);
        UpdatedAt = DateTime.UtcNow;
        return c;
    }

    public void RemoveCharacteristic(int seq)
    {
        EnsureDraft();
        Find(seq).MarkDeleted();
        UpdatedAt = DateTime.UtcNow;
    }

    private InspectionCharacteristic Find(int seq)
        => Characteristics.FirstOrDefault(c => c.Seq == seq) ?? throw MfgRules.Invalid($"Characteristic {seq} does not exist.");

    /// <summary>BR-QC-01: a measurement needs Min &lt;= Max (a bound may be left open, not both); an attribute carries no range; the sequence is unique.</summary>
    private void Check(InspectionCharacteristicValues v, int? exceptSeq)
    {
        if (v.Seq <= 0 || Characteristics.Any(c => c.Seq == v.Seq && c.Seq != exceptSeq))
            throw MfgRules.Invalid($"Characteristic number {v.Seq} is not valid or is used twice.");
        if (!Enum.IsDefined(v.Kind)) throw MfgRules.Invalid("A characteristic is a measurement or an attribute.");
        MasterCode.Name(v.Name, 200);
        if (v.Kind == CharacteristicKind.Measurement)
        {
            if (v.MinValue is null && v.MaxValue is null) throw MfgRules.Invalid("A measurement needs a minimum, a maximum or both.");
            if (v.MinValue is { } min && v.MaxValue is { } max && min > max) throw MfgRules.Invalid("The minimum of a measurement cannot be greater than its maximum.");
        }
        else if (v.MinValue is not null || v.MaxValue is not null)
            throw MfgRules.Invalid("An attribute (pass / fail) has no minimum or maximum.");
    }

    // ── Life cycle ──────────────────────────────────────────────────────────────────────────────────────────

    public void Approve(string approvedBy, DateTime approvedAt)
    {
        EnsureDraft();
        if (Characteristics.Count == 0) throw MfgRules.Invalid("An inspection plan needs at least one characteristic.");
        Status = VersionStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Obsolete(DateOnly effectiveTo)
    {
        MfgRules.ThrowIf(Status != VersionStatus.Approved, MfgErrors.Bom002);
        if (effectiveTo < EffectiveFrom) throw MfgRules.Invalid("The end of the validity period cannot be before its start.");
        Status = VersionStatus.Obsolete;
        EffectiveTo = EffectiveTo is { } current && current < effectiveTo ? current : effectiveTo;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEffectiveToOfObsolete(DateOnly effectiveTo)
    {
        MfgRules.ThrowIf(Status != VersionStatus.Obsolete, MfgErrors.Bom002);
        if (effectiveTo < EffectiveFrom) throw MfgRules.Invalid("The end of the validity period cannot be before its start.");
        EffectiveTo = effectiveTo;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CloseOn(DateOnly effectiveTo)
    {
        EffectiveTo = effectiveTo;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>A new Draft version of the same series (same code, type and item) with a copy of every characteristic.</summary>
    public InspectionPlan CopyAsNewDraft(int newVersion, DateOnly effectiveFrom)
    {
        var copy = Create(PlantId, ItemId, ItemCode, ItemName, Code, Type, newVersion, SamplingMethod, SampleValue, AllowedMinorDefects, effectiveFrom, null, Description);
        foreach (var c in Characteristics) copy._allCharacteristics.Add(InspectionCharacteristic.Create(copy.Id, c.ToValues()));
        return copy;
    }

    public void MarkDeleted()
    {
        EnsureDraft();
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsEffectiveOn(DateOnly date) => Status == VersionStatus.Approved && EffectiveFrom <= date && (EffectiveTo is null || date <= EffectiveTo);
}
