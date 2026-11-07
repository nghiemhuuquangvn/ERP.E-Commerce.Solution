using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>The editable values of one routing operation (BA spec 01 §5.9, §7.2). The work center code is a snapshot for display.</summary>
public sealed record RoutingOperationValues(
    int OperationSeq,
    Guid? StandardOperationId,
    string Name,
    Guid WorkCenterId,
    string WorkCenterCode,
    decimal SetupTimeMinutes,
    decimal RunTimeMinutes,
    RunTimeBasis RunTimeBasis,
    decimal? BatchSize,
    int QueueTimeMinutes,
    int MoveTimeMinutes,
    bool IsSubcontract,
    string? Instructions);

/// <summary>One step of a routing: where it is done and how long it takes.</summary>
public class RoutingOperation : BaseEntity
{
    private RoutingOperation() { }

    public Guid RoutingId { get; private set; }
    public int OperationSeq { get; private set; }
    public Guid? StandardOperationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid WorkCenterId { get; private set; }
    public string WorkCenterCode { get; private set; } = string.Empty;
    public decimal SetupTimeMinutes { get; private set; }
    public decimal RunTimeMinutes { get; private set; }
    public RunTimeBasis RunTimeBasis { get; private set; } = RunTimeBasis.PerUnit;
    public decimal? BatchSize { get; private set; }
    public int QueueTimeMinutes { get; private set; }
    public int MoveTimeMinutes { get; private set; }
    public bool IsSubcontract { get; private set; }
    public string? Instructions { get; private set; }

    internal static RoutingOperation Create(Guid routingId, RoutingOperationValues values)
    {
        var op = new RoutingOperation { Id = Guid.NewGuid(), RoutingId = routingId };
        op.Apply(values);
        return op;
    }

    internal void Apply(RoutingOperationValues v)
    {
        OperationSeq = v.OperationSeq;
        StandardOperationId = v.StandardOperationId;
        Name = v.Name;
        WorkCenterId = v.WorkCenterId;
        WorkCenterCode = v.WorkCenterCode;
        SetupTimeMinutes = v.SetupTimeMinutes;
        RunTimeMinutes = v.RunTimeMinutes;
        RunTimeBasis = v.RunTimeBasis;
        BatchSize = v.RunTimeBasis == RunTimeBasis.PerBatch ? v.BatchSize : null;
        QueueTimeMinutes = v.QueueTimeMinutes;
        MoveTimeMinutes = v.MoveTimeMinutes;
        IsSubcontract = v.IsSubcontract;
        Instructions = v.Instructions;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void SetSeq(int seq) { OperationSeq = seq; UpdatedAt = DateTime.UtcNow; }
    internal void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }

    public RoutingOperationValues ToValues() => new(OperationSeq, StandardOperationId, Name, WorkCenterId, WorkCenterCode, SetupTimeMinutes, RunTimeMinutes,
        RunTimeBasis, BatchSize, QueueTimeMinutes, MoveTimeMinutes, IsSubcontract, Instructions);
}

/// <summary>
/// A routing: the operations that make one item, in one version (BR-RT-03: Draft -> Approved -> Obsolete, approved versions never change, a change is
/// a new version made by <see cref="CopyAsNewDraft"/>). An item may have no routing at all (BR-RT-07).
/// </summary>
public class Routing : BaseEntity, IVersionedDocument
{
    private readonly List<RoutingOperation> _allOperations = [];

    private Routing() { }

    public Guid ItemId { get; private set; }
    public Guid PlantId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public VersionStatus Status { get; private set; } = VersionStatus.Draft;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    public IReadOnlyCollection<RoutingOperation> Operations => _allOperations.Where(o => !o.IsDeleted).OrderBy(o => o.OperationSeq).ToList();

    /// <summary>Persistence access to the loaded children (EF fills the field).</summary>
    public IReadOnlyList<RoutingOperation> AllOperations => _allOperations;

    public static Routing Create(Guid itemId, Guid plantId, string itemCode, string itemName, int version, DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        var routing = new Routing { Id = Guid.NewGuid(), ItemId = itemId, PlantId = plantId, ItemCode = itemCode, ItemName = itemName, Version = version };
        routing.SetHeader(effectiveFrom, effectiveTo, description);
        return routing;
    }

    public void EnsureDraft() => MfgRules.ThrowIf(Status != VersionStatus.Draft, MfgErrors.Bom002);

    public void UpdateHeader(DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        EnsureDraft();
        SetHeader(effectiveFrom, effectiveTo, description);
    }

    private void SetHeader(DateOnly effectiveFrom, DateOnly? effectiveTo, string? description)
    {
        if (effectiveTo is { } to && to < effectiveFrom) throw MfgRules.Invalid("The end of the validity period cannot be before its start.");
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Operations ──────────────────────────────────────────────────────────────────────────────────────────

    public RoutingOperation AddOperation(RoutingOperationValues values)
    {
        EnsureDraft();
        Check(values, exceptSeq: null);
        var op = RoutingOperation.Create(Id, values);
        _allOperations.Add(op);
        UpdatedAt = DateTime.UtcNow;
        return op;
    }

    /// <summary>The next free sequence number in steps of 10 (the usual way to number operations).</summary>
    public int NextSeq() => (Operations.Count == 0 ? 0 : Operations.Max(o => o.OperationSeq)) / 10 * 10 + 10;

    public RoutingOperation UpdateOperation(int seq, RoutingOperationValues values)
    {
        EnsureDraft();
        var op = Find(seq);
        Check(values, exceptSeq: seq);
        op.Apply(values);
        UpdatedAt = DateTime.UtcNow;
        return op;
    }

    public void RemoveOperation(int seq)
    {
        EnsureDraft();
        Find(seq).MarkDeleted();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The order of the screen (drag and drop): the operations get 10, 20, 30 ... in the order of <paramref name="orderedSeqs"/>, which must list every operation once.
    /// Two steps because the sequence is unique per routing and rows would clash while they swap numbers: <see cref="ParkForRenumber"/>, save, then this.
    /// </summary>
    public void Renumber(IReadOnlyList<int> orderedSeqs)
    {
        EnsureDraft();
        CheckOrder(orderedSeqs);
        var parked = Operations.All(o => o.OperationSeq < 0);
        var byOldSeq = Operations.ToDictionary(o => parked ? -o.OperationSeq : o.OperationSeq);
        for (var i = 0; i < orderedSeqs.Count; i++) byOldSeq[orderedSeqs[i]].SetSeq((i + 1) * 10);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Step one of a renumbering: validates the new order and moves every operation to its negative number (nothing else uses those).</summary>
    public void ParkForRenumber(IReadOnlyList<int> orderedSeqs)
    {
        EnsureDraft();
        CheckOrder(orderedSeqs);
        foreach (var op in Operations) op.SetSeq(-op.OperationSeq);
    }

    private void CheckOrder(IReadOnlyList<int> orderedSeqs)
    {
        var current = Operations.Select(o => Math.Abs(o.OperationSeq)).OrderBy(s => s).ToList();
        if (orderedSeqs.Count != current.Count || !orderedSeqs.OrderBy(s => s).SequenceEqual(current))
            throw MfgRules.Invalid("The new order must list every operation of the routing exactly once.");
    }

    private RoutingOperation Find(int seq)
        => Operations.FirstOrDefault(o => o.OperationSeq == seq) ?? throw MfgRules.Invalid($"Operation {seq} does not exist.");

    /// <summary>BR-RT-01 (sequence), BR-RT-02 (times and batch).</summary>
    private void Check(RoutingOperationValues v, int? exceptSeq)
    {
        if (v.OperationSeq <= 0 || Operations.Any(o => o.OperationSeq == v.OperationSeq && o.OperationSeq != exceptSeq))
            throw MfgRules.Fail(MfgErrors.Rt001, ("seq", v.OperationSeq));
        if (v.SetupTimeMinutes < 0 || v.RunTimeMinutes <= 0 || !Enum.IsDefined(v.RunTimeBasis)
            || (v.RunTimeBasis == RunTimeBasis.PerBatch && v.BatchSize is null or <= 0)
            || v.QueueTimeMinutes < 0 || v.MoveTimeMinutes < 0)
            throw MfgRules.Fail(MfgErrors.Rt002);
        MasterCode.Name(v.Name, 200);
    }

    /// <summary>BR-RT-03 / BR-RT-06 on the center of an operation: active and of this plant (<c>MFG_RT_003</c>); a subcontracted operation needs a Subcontract center (<c>MFG_RT_004</c>).</summary>
    public static void CheckWorkCenter(WorkCenter center, Guid plantId, bool isSubcontract)
    {
        if (center.PlantId != plantId || !center.IsActive) throw MfgRules.Fail(MfgErrors.Rt003);
        if (isSubcontract && center.Type != WorkCenterType.Subcontract) throw MfgRules.Fail(MfgErrors.Rt004);
    }

    // ── Life cycle ──────────────────────────────────────────────────────────────────────────────────────────

    public void Approve(string approvedBy, DateTime approvedAt)
    {
        EnsureDraft();
        if (Operations.Count == 0) throw MfgRules.Invalid("A routing needs at least one operation.");
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

    public Routing CopyAsNewDraft(int newVersion, DateOnly effectiveFrom)
    {
        var copy = Create(ItemId, PlantId, ItemCode, ItemName, newVersion, effectiveFrom, null, Description);
        foreach (var op in Operations) copy._allOperations.Add(RoutingOperation.Create(copy.Id, op.ToValues()));
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
