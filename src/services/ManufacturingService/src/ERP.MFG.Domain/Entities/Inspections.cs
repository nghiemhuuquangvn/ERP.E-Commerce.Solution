using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>One value of an inspection: the measurement of one sample unit of a characteristic, or the pass / fail of an attribute (sample number 1).</summary>
public class InspectionResult : BaseEntity
{
    private InspectionResult() { }

    public Guid InspectionId { get; private set; }
    public Guid CharacteristicId { get; private set; }
    public int SampleNo { get; private set; }
    public decimal? MeasuredValue { get; private set; }
    public bool? AttributePassed { get; private set; }
    public bool IsPassed { get; private set; }
    public string? Note { get; private set; }

    internal static InspectionResult Create(Guid inspectionId, InspectionCharacteristic c, InspectionResultInput i)
    {
        var r = new InspectionResult { Id = Guid.NewGuid(), InspectionId = inspectionId, CharacteristicId = c.Id, SampleNo = i.SampleNo };
        r.Apply(c, i);
        return r;
    }

    internal void Apply(InspectionCharacteristic c, InspectionResultInput i)
    {
        MeasuredValue = c.Kind == CharacteristicKind.Measurement ? i.Value : null;
        AttributePassed = c.Kind == CharacteristicKind.Attribute ? i.Passed : null;
        IsPassed = c.Kind == CharacteristicKind.Measurement ? c.InRange(i.Value!.Value) : i.Passed == true;
        Note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// An inspection document (BR-QC-02..06): the final inspection (FQC) of a received lot or the in-process inspection (IPQC) of the Good output of one operation
/// report. It is created Pending with the sample size of its plan, the inspector saves values as a draft, then submits: Passed or Failed, immutable from then on
/// (<c>MFG_QC_002</c>). A failed one is corrected by a re-inspection (a new document that points back at it).
/// </summary>
public class Inspection : BaseEntity
{
    private readonly List<InspectionResult> _allResults = [];

    private Inspection() { }

    public string DocNo { get; private set; } = string.Empty;
    public Guid PlantId { get; private set; }
    public InspectionType Type { get; private set; }
    public Guid PlanId { get; private set; }
    public string PlanCode { get; private set; } = string.Empty;
    public int PlanVersion { get; private set; }
    public Guid OrderId { get; private set; }
    public string OrderNo { get; private set; } = string.Empty;
    public Guid? OrderOperationId { get; private set; }
    public int? OperationSeq { get; private set; }
    public Guid? OperationReportId { get; private set; }
    public Guid? ReceiptDocumentId { get; private set; }
    public Guid? ReceiptLineId { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public Guid? LotId { get; private set; }
    public string? LotNumber { get; private set; }
    public decimal LotQty { get; private set; }
    public decimal SampleQty { get; private set; }
    public InspectionStatus Status { get; private set; } = InspectionStatus.Pending;
    public string? InspectedBy { get; private set; }
    public DateTime? InspectedAt { get; private set; }
    public Guid? ReinspectionOfId { get; private set; }
    public Guid? ReinspectedById { get; private set; }

    /// <summary>True on a Failed inspection: a non-conformance report is due (P4-4 creates it).</summary>
    public bool NcrRequired { get; private set; }

    /// <summary>FQC Pass: when the lot was released from quality hold through this inspection.</summary>
    public DateTime? ReleasedAt { get; private set; }

    public IReadOnlyCollection<InspectionResult> Results => _allResults.Where(r => !r.IsDeleted).OrderBy(r => r.CharacteristicId).ThenBy(r => r.SampleNo).ToList();

    /// <summary>Persistence access to the loaded children (EF fills the field).</summary>
    public IReadOnlyList<InspectionResult> AllResults => _allResults;

    public bool IsFqc => Type == InspectionType.Fqc;

    /// <summary>The inspection still decides something: Pending, or Failed (the lot waits for its re-inspection / NCR).</summary>
    public bool HasResult => Status is InspectionStatus.Passed or InspectionStatus.Failed;

    /// <summary>BR-QC-02: the final inspection of one line of a Posted receipt, for the lot the Warehouse created.</summary>
    public static Inspection CreateFqc(string docNo, InspectionPlan plan, Guid orderId, string orderNo, Guid receiptDocumentId, Guid receiptLineId, Guid itemId, string itemCode, string itemName,
        Guid lotId, string? lotNumber, decimal lotQty)
        => new()
        {
            Id = Guid.NewGuid(), DocNo = docNo, PlantId = plan.PlantId, Type = InspectionType.Fqc, PlanId = plan.Id, PlanCode = plan.Code, PlanVersion = plan.Version,
            OrderId = orderId, OrderNo = orderNo, ReceiptDocumentId = receiptDocumentId, ReceiptLineId = receiptLineId, ItemId = itemId, ItemCode = itemCode, ItemName = itemName,
            LotId = lotId, LotNumber = lotNumber, LotQty = lotQty, SampleQty = QualityRules.SampleSize(plan.SamplingMethod, plan.SampleValue, lotQty),
        };

    /// <summary>BR-QC-02: the in-process inspection of the Good output of one operation report.</summary>
    public static Inspection CreateIpqc(string docNo, InspectionPlan plan, Guid orderId, string orderNo, Guid operationId, int operationSeq, Guid reportId, Guid itemId, string itemCode, string itemName, decimal goodQty)
        => new()
        {
            Id = Guid.NewGuid(), DocNo = docNo, PlantId = plan.PlantId, Type = InspectionType.Ipqc, PlanId = plan.Id, PlanCode = plan.Code, PlanVersion = plan.Version,
            OrderId = orderId, OrderNo = orderNo, OrderOperationId = operationId, OperationSeq = operationSeq, OperationReportId = reportId, ItemId = itemId, ItemCode = itemCode, ItemName = itemName,
            LotQty = goodQty, SampleQty = QualityRules.SampleSize(plan.SamplingMethod, plan.SampleValue, goodQty),
        };

    public void EnsurePending() => MfgRules.ThrowIf(Status != InspectionStatus.Pending, MfgErrors.Qc002);

    /// <summary>The sample size must be the one of the plan (<c>MFG_QC_001</c>); a client that sends a figure of its own is refused.</summary>
    public void CheckSample(decimal? sampleQty) => MfgRules.ThrowIf(sampleQty is { } s && s != SampleQty, MfgErrors.Qc001, ("sample", SampleQty));

    /// <summary>
    /// Saves values as a draft (the inspection stays Pending). A measurement needs a value per sample unit (1..n), an attribute a pass / fail on sample 1; a value that is
    /// typed again replaces the earlier one.
    /// </summary>
    public void SaveDraft(InspectionPlan plan, IEnumerable<InspectionResultInput> inputs)
    {
        EnsurePending();
        var units = QualityRules.Units(SampleQty);
        foreach (var i in inputs)
        {
            var c = plan.Characteristics.FirstOrDefault(x => x.Id == i.CharacteristicId) ?? throw MfgRules.Invalid("A result names a characteristic that is not in the plan.");
            if (c.Kind == CharacteristicKind.Measurement)
            {
                if (i.Value is null) throw MfgRules.Invalid($"A measured value is needed for {c.Name}.");
                if (i.SampleNo < 1 || i.SampleNo > units) throw MfgRules.Invalid($"The sample number of {c.Name} must be between 1 and {units}.");
            }
            else
            {
                if (i.Passed is null) throw MfgRules.Invalid($"Pass or fail is needed for {c.Name}.");
                if (i.SampleNo != 1) throw MfgRules.Invalid($"{c.Name} is judged once for the sample (sample number 1).");
            }

            var existing = _allResults.FirstOrDefault(r => !r.IsDeleted && r.CharacteristicId == c.Id && r.SampleNo == i.SampleNo);
            if (existing is null) _allResults.Add(InspectionResult.Create(Id, c, i));
            else existing.Apply(c, i);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>BR-QC-04: the verdict of the values entered so far. <c>MFG_QC_003</c> while some characteristic still lacks values.</summary>
    public QualityOutcome Evaluate(InspectionPlan plan)
    {
        var outcome = QualityRules.Evaluate(plan.Characteristics, Results, SampleQty, plan.AllowedMinorDefects);
        MfgRules.ThrowIf(outcome.Missing.Count > 0, MfgErrors.Qc003);
        return outcome;
    }

    /// <summary>The inspector submits: Passed or Failed, and the document is final (BR-QC-06). A failed one asks for a non-conformance report.</summary>
    public void Complete(QualityOutcome outcome, string by, DateTime now)
    {
        EnsurePending();
        Status = outcome.Passed ? InspectionStatus.Passed : InspectionStatus.Failed;
        InspectedBy = by;
        InspectedAt = now;
        NcrRequired = !outcome.Passed;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new InspectionCompletedEvent
        {
            InspectionId = Id, DocNo = DocNo, Type = Type.ToString(), Status = Status.ToString(), OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, ItemId = ItemId, ItemCode = ItemCode,
            LotId = LotId, LotNumber = LotNumber, LotQty = LotQty, SampleQty = SampleQty, NcrRequired = NcrRequired, InspectedBy = by, OperationSeq = OperationSeq,
            ReinspectionOfId = ReinspectionOfId,
        });
    }

    /// <summary>FQC Pass: the lot has left quality hold through this inspection.</summary>
    public void MarkReleased(DateTime now) { ReleasedAt = now; UpdatedAt = DateTime.UtcNow; }

    /// <summary>The receipt line / report this inspection looked at was reversed while it was still Pending (P4-3): nothing is left to inspect.</summary>
    public void Cancel()
    {
        EnsurePending();
        Status = InspectionStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// BR-QC-06: a Failed inspection that was not re-inspected yet is repeated by a new document that points back at it (same lot, same plan version).
    /// </summary>
    public Inspection CreateReinspection(string docNo)
    {
        MfgRules.ThrowIf(Status != InspectionStatus.Failed, MfgErrors.Qc002);
        if (ReinspectedById is not null) throw MfgRules.Invalid($"Inspection {DocNo} has already been re-inspected.");
        var again = new Inspection
        {
            Id = Guid.NewGuid(), DocNo = docNo, PlantId = PlantId, Type = Type, PlanId = PlanId, PlanCode = PlanCode, PlanVersion = PlanVersion, OrderId = OrderId, OrderNo = OrderNo,
            OrderOperationId = OrderOperationId, OperationSeq = OperationSeq, OperationReportId = OperationReportId, ReceiptDocumentId = ReceiptDocumentId, ReceiptLineId = ReceiptLineId,
            ItemId = ItemId, ItemCode = ItemCode, ItemName = ItemName, LotId = LotId, LotNumber = LotNumber, LotQty = LotQty, SampleQty = SampleQty, ReinspectionOfId = Id,
        };
        ReinspectedById = again.Id;
        UpdatedAt = DateTime.UtcNow;
        return again;
    }
}
