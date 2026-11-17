using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>One line the quality manager typed: what to do with how many units (BR-NCR-02). A Scrap line names its Scrap reason.</summary>
public sealed record DispositionInput(DispositionType Type, decimal Qty, Guid? ReasonCodeId, string? Note);

/// <summary>One disposition line of a non-conformance report (BR-NCR-02 / 03). It is executed on its own, in any order.</summary>
public class NcrDisposition : BaseEntity
{
    private NcrDisposition() { }

    public Guid NcrId { get; private set; }
    public int LineNo { get; private set; }
    public DispositionType Type { get; private set; }
    public decimal Qty { get; private set; }
    public Guid? ReasonCodeId { get; private set; }
    public string? Note { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    /// <summary>Rework line: the rework order made for it. The line is started from then on and executed when that order is Completed.</summary>
    public Guid? ReworkOrderId { get; private set; }

    /// <summary>The Warehouse key of the movement (Scrap: the key itself; UseAsIs: the key with Q1 / Q2).</summary>
    public string? MovementKey { get; private set; }

    /// <summary>UseAsIs: the lot that was released (the whole lot, or the sub-lot a partial move created).</summary>
    public Guid? ResultLotId { get; private set; }
    public string? ResultLotNumber { get; private set; }
    public bool IsExecuted { get; private set; }
    public DateTime? ExecutedAt { get; private set; }
    public string? ExecutedBy { get; private set; }

    /// <summary>Executed, or a rework order was made for it: the line can no longer be replaced.</summary>
    public bool IsStarted => IsExecuted || ReworkOrderId is not null;

    internal static NcrDisposition Create(Guid ncrId, int lineNo, DispositionInput i, string? approvedBy, DateTime? approvedAt) => new()
    {
        Id = Guid.NewGuid(), NcrId = ncrId, LineNo = lineNo, Type = i.Type, Qty = i.Qty, ReasonCodeId = i.ReasonCodeId,
        Note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim(), ApprovedBy = approvedBy, ApprovedAt = approvedAt,
    };

    internal void MarkReworkOrder(Guid? orderId) { ReworkOrderId = orderId; UpdatedAt = DateTime.UtcNow; }

    internal void MarkExecuted(string? movementKey, Guid? resultLotId, string? resultLotNumber, string by, DateTime now)
    {
        MovementKey = movementKey;
        ResultLotId = resultLotId;
        ResultLotNumber = resultLotNumber;
        IsExecuted = true;
        ExecutedAt = now;
        ExecutedBy = by;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }
}

/// <summary>
/// A non-conformance report (BR-NCR-01..04, BA spec 01 §5.15): the record of units that did not pass inspection and what is done with them. It is created by a Failed
/// inspection (FQC: a lot in the quarantine warehouse; IPQC: output of an operation that is held) or by hand from an operation report (record only). The quality manager
/// splits the quantity into Rework / Scrap / UseAsIs lines (the sum is the non-conforming quantity), the lines are executed one by one and the NCR closes with the last one.
/// </summary>
public class NonConformance : BaseEntity
{
    private readonly List<NcrDisposition> _dispositions = [];

    private NonConformance() { }

    public string NcrNo { get; private set; } = string.Empty;
    public Guid PlantId { get; private set; }
    public NcrSourceType SourceType { get; private set; }
    public Guid? InspectionId { get; private set; }
    public Guid? OperationReportId { get; private set; }
    public Guid OrderId { get; private set; }
    public string OrderNo { get; private set; } = string.Empty;
    public int? OperationSeq { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public Guid? LotId { get; private set; }
    public string? LotNumber { get; private set; }
    public Guid? ReceiptDocumentId { get; private set; }
    public Guid? ReceiptLineId { get; private set; }
    public decimal NonconformingQty { get; private set; }
    public NcrStatus Status { get; private set; } = NcrStatus.Open;
    public DateTime? ClosedAt { get; private set; }
    public string? ClosedBy { get; private set; }

    public IReadOnlyCollection<NcrDisposition> Dispositions => _dispositions.Where(d => !d.IsDeleted).OrderBy(d => d.LineNo).ToList();

    /// <summary>Persistence access to the loaded children (EF fills the field).</summary>
    public IReadOnlyList<NcrDisposition> AllDispositions => _dispositions;

    /// <summary>The NCR still has work: Open or InDisposition (it blocks the close of the order).</summary>
    public bool IsOpen => Status is NcrStatus.Open or NcrStatus.InDisposition;

    /// <summary>Output of an operation that failed its in-process inspection: no lot in the Warehouse, no rework (<c>MFG_NCR_003</c>).</summary>
    public bool IsInProcess => SourceType == NcrSourceType.Inspection && OperationSeq is not null && LotId is null;

    /// <summary>A person raised it from an operation report: it records the finding and its dispositions, nothing moves.</summary>
    public bool IsRecordOnly => SourceType == NcrSourceType.OperationReport;

    public bool HasStarted => _dispositions.Any(d => !d.IsDeleted && d.IsStarted);

    /// <summary>T4.10.1: a Failed inspection asks for its NCR; the non-conforming quantity is the whole inspected lot (an FQC result decides the whole lot).</summary>
    public static NonConformance CreateFromInspection(string ncrNo, Inspection inspection, string by)
    {
        MfgRules.ThrowIf(inspection.Status != InspectionStatus.Failed, MfgErrors.Qc002);
        var ncr = new NonConformance
        {
            Id = Guid.NewGuid(), NcrNo = ncrNo, PlantId = inspection.PlantId, SourceType = NcrSourceType.Inspection, InspectionId = inspection.Id, OrderId = inspection.OrderId,
            OrderNo = inspection.OrderNo, OperationSeq = inspection.OperationSeq, ItemId = inspection.ItemId, ItemCode = inspection.ItemCode, ItemName = inspection.ItemName,
            LotId = inspection.LotId, LotNumber = inspection.LotNumber, ReceiptDocumentId = inspection.ReceiptDocumentId, ReceiptLineId = inspection.ReceiptLineId,
            NonconformingQty = inspection.LotQty, Description = $"Inspection {inspection.DocNo} failed", CreatedBy = by, UpdatedBy = by,
        };
        ncr.AddDomainEvent(ncr.ToCreatedEvent(by));
        return ncr;
    }

    /// <summary>T4.10.1: raised by hand from an operation report; the quantity may not exceed what the report counted (BR-NCR-01).</summary>
    public static NonConformance CreateManual(string ncrNo, OperationReport report, string orderNo, Guid itemId, string itemCode, string itemName, decimal qty, string description, string by)
    {
        var reported = report.GoodQty + report.ScrapQty + report.ReworkQty;
        if (qty <= 0 || Math.Round(qty, 6) != qty) throw MfgRules.Invalid("The non-conforming quantity must be greater than 0.");
        if (qty > reported) throw MfgRules.Invalid($"The non-conforming quantity cannot exceed the {reported:0.######} units of the report {report.DocNo}.");
        if (string.IsNullOrWhiteSpace(description)) throw MfgRules.Invalid("Describe what is wrong.");
        var ncr = new NonConformance
        {
            Id = Guid.NewGuid(), NcrNo = ncrNo, PlantId = report.PlantId, SourceType = NcrSourceType.OperationReport, OperationReportId = report.Id, OrderId = report.OrderId, OrderNo = orderNo,
            OperationSeq = report.OperationSeq, ItemId = itemId, ItemCode = itemCode, ItemName = itemName, NonconformingQty = qty, Description = description.Trim(), CreatedBy = by, UpdatedBy = by,
        };
        ncr.AddDomainEvent(ncr.ToCreatedEvent(by));
        return ncr;
    }

    /// <summary>
    /// BR-NCR-02 / 03: replaces the disposition lines while none has started. The sum must be the non-conforming quantity (<c>MFG_NCR_001</c>), a UseAsIs line needs the
    /// approval right of the caller (<c>MFG_NCR_002</c>), output held by an in-process inspection cannot be reworked (<c>MFG_NCR_003</c>).
    /// </summary>
    public void SetDispositions(IReadOnlyList<DispositionInput> lines, bool canApproveDeviation, string by, DateTime now)
    {
        MfgRules.ThrowIf(!IsOpen, MfgErrors.Ncr004, ("ncr", NcrNo), ("reason", "đã đóng hoặc đã hủy, không thể sửa cách xử lý"));
        MfgRules.ThrowIf(HasStarted, MfgErrors.Ncr004, ("ncr", NcrNo), ("reason", "đã bắt đầu thực hiện một dòng xử lý, không thể thay thế các dòng"));
        NcrRules.CheckDispositions(lines, NonconformingQty, IsInProcess, canApproveDeviation);
        foreach (var d in _dispositions.Where(x => !x.IsDeleted)) d.MarkDeleted();
        var n = 0;
        foreach (var l in lines)
            _dispositions.Add(NcrDisposition.Create(Id, ++n, l, l.Type == DispositionType.UseAsIs ? by : null, l.Type == DispositionType.UseAsIs ? now : null));
        Status = NcrStatus.InDisposition;
        Touch(by);
    }

    public NcrDisposition LineOf(int lineNo)
        => _dispositions.FirstOrDefault(d => d.LineNo == lineNo && !d.IsDeleted) ?? throw MfgRules.Invalid($"{NcrNo} has no disposition line {lineNo}.");

    /// <summary>A line can be executed once, while the NCR is InDisposition.</summary>
    public NcrDisposition EnsureExecutable(int lineNo)
    {
        MfgRules.ThrowIf(Status != NcrStatus.InDisposition, MfgErrors.Ncr004, ("ncr", NcrNo), ("reason", "chưa có cách xử lý hoặc đã đóng"));
        var line = LineOf(lineNo);
        MfgRules.ThrowIf(line.IsExecuted, MfgErrors.Ncr004, ("ncr", NcrNo), ("reason", $"dòng {lineNo} đã được thực hiện"));
        return line;
    }

    /// <summary>A rework order was made for the line: from now on the lines cannot be replaced.</summary>
    public void MarkReworkStarted(int lineNo, Guid reworkOrderId, string by)
    {
        var line = EnsureExecutable(lineNo);
        MfgRules.ThrowIf(line.Type != DispositionType.Rework, MfgErrors.Ncr004, ("ncr", NcrNo), ("reason", $"dòng {lineNo} không phải dòng làm lại"));
        MfgRules.ThrowIf(line.ReworkOrderId is not null, MfgErrors.Ncr004, ("ncr", NcrNo), ("reason", $"dòng {lineNo} đã có lệnh làm lại"));
        line.MarkReworkOrder(reworkOrderId);
        Touch(by);
    }

    /// <summary>The rework order of a line was cancelled before any work: the line can be executed again.</summary>
    public bool ClearReworkOrder(Guid reworkOrderId, string by)
    {
        var line = _dispositions.FirstOrDefault(d => !d.IsDeleted && !d.IsExecuted && d.ReworkOrderId == reworkOrderId);
        if (line is null) return false;
        line.MarkReworkOrder(null);
        Touch(by);
        return true;
    }

    /// <summary>The line was carried out (Warehouse movement done / rework order completed / record-only line marked done). Closes the NCR with the last line. Returns true when it closed.</summary>
    public bool MarkExecuted(int lineNo, string? movementKey, Guid? resultLotId, string? resultLotNumber, string by, DateTime now)
    {
        var line = EnsureExecutable(lineNo);
        line.MarkExecuted(movementKey, resultLotId, resultLotNumber, by, now);
        Touch(by);
        if (_dispositions.Any(d => !d.IsDeleted && !d.IsExecuted)) return false;
        Status = NcrStatus.Closed;
        ClosedAt = now;
        ClosedBy = by;
        AddDomainEvent(ToClosedEvent(by));
        return true;
    }

    /// <summary>A re-inspection of the failed inspection replaces this NCR before any line started (BR-QC-06).</summary>
    public void Cancel(string by)
    {
        MfgRules.ThrowIf(!IsOpen, MfgErrors.Ncr004, ("ncr", NcrNo), ("reason", "đã đóng hoặc đã hủy"));
        MfgRules.ThrowIf(HasStarted, MfgErrors.Ncr004, ("ncr", NcrNo), ("reason", "đã bắt đầu thực hiện, hãy xử lý tiếp bằng phiếu này thay vì kiểm tra lại"));
        Status = NcrStatus.Cancelled;
        Touch(by);
    }

    private void Touch(string by) { UpdatedAt = DateTime.UtcNow; UpdatedBy = by; }

    private NcrCreatedEvent ToCreatedEvent(string by) => new()
    {
        NcrId = Id, NcrNo = NcrNo, PlantId = PlantId, SourceType = SourceType.ToString(), InspectionId = InspectionId, OrderId = OrderId, OrderNo = OrderNo, ItemId = ItemId, ItemCode = ItemCode,
        LotId = LotId, LotNumber = LotNumber, NonconformingQty = NonconformingQty, CreatedBy = by,
    };

    private NcrClosedEvent ToClosedEvent(string by)
    {
        decimal Sum(DispositionType t) => _dispositions.Where(d => !d.IsDeleted && d.Type == t).Sum(d => d.Qty);
        return new()
        {
            NcrId = Id, NcrNo = NcrNo, PlantId = PlantId, OrderId = OrderId, OrderNo = OrderNo, ItemId = ItemId, ItemCode = ItemCode, NonconformingQty = NonconformingQty,
            ReworkQty = Sum(DispositionType.Rework), ScrapQty = Sum(DispositionType.Scrap), UseAsIsQty = Sum(DispositionType.UseAsIs), ClosedBy = by,
        };
    }
}
