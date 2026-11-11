using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

// Execution of an operation (BA spec 01 §5.12, §7.3): the time an employee works on it and the output reported on it.

/// <summary>The facts of an employee that start a time log (a snapshot for display; the rate is looked up from the crew membership).</summary>
public sealed record TimeLogWorker(Guid EmployeeId, string EmployeeNumber, string EmployeeName, Guid? MachineId, decimal? LaborRatePerHour);

/// <summary>
/// One employee's work on one operation (BR-OP-02, BR-OP-05). Live logs are opened with <see cref="StartNow"/>, paused, resumed and stopped; a forgotten session is
/// entered afterwards with <see cref="Manual"/>. Duration = End - Start - paused minutes.
/// </summary>
public class LaborTimeLog : BaseEntity
{
    private LaborTimeLog() { }

    public Guid OrderId { get; private set; }
    public Guid OrderOperationId { get; private set; }
    public int OperationSeq { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string EmployeeNumber { get; private set; } = string.Empty;
    public string EmployeeName { get; private set; } = string.Empty;
    public Guid? MachineId { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime? EndAt { get; private set; }

    /// <summary>When the current pause began (null = not paused).</summary>
    public DateTime? PausedAt { get; private set; }

    public decimal PausedMinutes { get; private set; }
    public decimal? DurationMinutes { get; private set; }
    public decimal? LaborRatePerHour { get; private set; }
    public bool IsManual { get; private set; }

    public bool IsOpen => EndAt is null;
    public bool IsRunning => EndAt is null && PausedAt is null;
    public bool IsPaused => EndAt is null && PausedAt is not null;

    /// <summary>A session that begins now (BR-OP-02).</summary>
    public static LaborTimeLog StartNow(Guid orderId, Guid operationId, int operationSeq, TimeLogWorker worker, DateTime now, string by) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, OrderOperationId = operationId, OperationSeq = operationSeq, EmployeeId = worker.EmployeeId,
        EmployeeNumber = worker.EmployeeNumber, EmployeeName = worker.EmployeeName, MachineId = worker.MachineId, LaborRatePerHour = worker.LaborRatePerHour,
        StartAt = now, CreatedBy = by, UpdatedBy = by,
    };

    /// <summary>BR-OP-05: a finished session entered afterwards. <c>End &gt; Start</c> and <c>Start &lt;= now</c>, else <c>MFG_OP_005</c>.</summary>
    public static LaborTimeLog Manual(Guid orderId, Guid operationId, int operationSeq, TimeLogWorker worker, DateTime start, DateTime end, DateTime now, string by)
    {
        MfgRules.ThrowIf(end <= start || start > now || end > now, MfgErrors.Op005);
        var log = StartNow(orderId, operationId, operationSeq, worker, start, by);
        log.IsManual = true;
        log.EndAt = end;
        log.DurationMinutes = Minutes(end - start);
        return log;
    }

    public void Pause(DateTime now)
    {
        MfgRules.ThrowIf(!IsRunning, MfgErrors.Op001);
        MfgRules.ThrowIf(now < StartAt, MfgErrors.Op005);
        PausedAt = now;
        Touch();
    }

    public void Resume(DateTime now)
    {
        MfgRules.ThrowIf(!IsPaused, MfgErrors.Op001);
        PausedMinutes += Minutes(now - PausedAt!.Value);
        PausedAt = null;
        Touch();
    }

    /// <summary>Closes the session (a pause still open ends with it) and computes the duration.</summary>
    public void Stop(DateTime now)
    {
        MfgRules.ThrowIf(!IsOpen, MfgErrors.Op001);
        MfgRules.ThrowIf(now < StartAt, MfgErrors.Op005);
        if (PausedAt is { } pausedAt)
        {
            PausedMinutes += Minutes(now - pausedAt);
            PausedAt = null;
        }
        EndAt = now;
        DurationMinutes = Math.Max(Minutes(now - StartAt) - PausedMinutes, 0m);
        Touch();
    }

    /// <summary>BR-OP-05: do two sessions of one person share any time? An open session runs to the end of time.</summary>
    public bool Overlaps(DateTime start, DateTime? end)
        => StartAt < (end ?? DateTime.MaxValue) && (EndAt ?? DateTime.MaxValue) > start;

    private void Touch() => UpdatedAt = DateTime.UtcNow;

    private static decimal Minutes(TimeSpan span) => Math.Round((decimal)span.TotalMinutes, 2);
}

/// <summary>
/// An operation report (BA spec 01 §7.3): good, scrap and rework quantities of one operation. It is stored Pending when a backflush document has to reach the Warehouse
/// first and applied to the order when that document is Posted (ADR Phase 3, decisions 3 and 8); a report with nothing to backflush is stored already Posted.
/// </summary>
public class OperationReport : BaseEntity
{
    private OperationReport() { }

    public string DocNo { get; private set; } = string.Empty;
    public Guid PlantId { get; private set; }
    public Guid OrderId { get; private set; }
    public string OrderNo { get; private set; } = string.Empty;
    public Guid OrderOperationId { get; private set; }
    public int OperationSeq { get; private set; }
    public DateTime ReportedAt { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public decimal GoodQty { get; private set; }
    public decimal ScrapQty { get; private set; }
    public decimal ReworkQty { get; private set; }
    public Guid? ScrapReasonId { get; private set; }
    public Guid? ReworkReasonId { get; private set; }
    public Guid? MachineId { get; private set; }
    public string ReportedBy { get; private set; } = string.Empty;
    public OperationReportStatus Status { get; private set; } = OperationReportStatus.Pending;

    /// <summary>The backflush document that must be Posted before this report counts (null = nothing to backflush).</summary>
    public Guid? BackflushDocumentId { get; private set; }

    public string? FailureCode { get; private set; }
    public string? FailureMessage { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? RequestHash { get; private set; }

    /// <summary>For the reversal of a report (P4-2, BR-OP-07): the report this one undoes. The quantities of a reversal are stored positive; this link says they leave the operation.</summary>
    public Guid? ReversalOfId { get; private set; }

    /// <summary>The report has been undone by a Posted reversal.</summary>
    public bool IsReversed { get; private set; }

    public bool IsReversal => ReversalOfId is not null;
    public string? Note { get; private set; }
    public DateTime? PostedAt { get; private set; }

    /// <summary>What the report adds to the cumulative output of the operation that backflush counts (BR-MAT-08): good plus scrap; rework goes round again and is not output.</summary>
    public decimal OutputQty => GoodQty + ScrapQty;

    public decimal TotalQty => GoodQty + ScrapQty + ReworkQty;

    public static OperationReport Create(string docNo, Guid plantId, Guid orderId, string orderNo, Guid operationId, int operationSeq, DateOnly postingDate, decimal good, decimal scrap,
        decimal rework, Guid? scrapReasonId, Guid? reworkReasonId, Guid? machineId, string? note, string? idempotencyKey, string? requestHash, DateTime now, string by)
    {
        MfgRules.ThrowIf(string.IsNullOrWhiteSpace(docNo), MfgErrors.Gen002);
        MfgRules.ThrowIf(good < 0 || scrap < 0 || rework < 0 || good + scrap + rework <= 0, MfgErrors.Op002);
        MfgRules.ThrowIf(scrap > 0 && scrapReasonId is null, MfgErrors.Op003);
        MfgRules.ThrowIf(rework > 0 && reworkReasonId is null, MfgErrors.Op003);
        return new OperationReport
        {
            Id = Guid.NewGuid(), DocNo = docNo, PlantId = plantId, OrderId = orderId, OrderNo = orderNo, OrderOperationId = operationId, OperationSeq = operationSeq,
            ReportedAt = now, PostingDate = postingDate, GoodQty = good, ScrapQty = scrap, ReworkQty = rework, ScrapReasonId = scrap > 0 ? scrapReasonId : null,
            ReworkReasonId = rework > 0 ? reworkReasonId : null, MachineId = machineId, ReportedBy = by, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            IdempotencyKey = idempotencyKey, RequestHash = requestHash, CreatedBy = by, UpdatedBy = by,
        };
    }

    /// <summary>
    /// The counter-record that undoes <paramref name="original"/> (P4-2, BR-OP-07): same operation and quantities (positive), linked by <c>ReversalOfId</c>, Pending until the reversal
    /// document that gives the backflush back has reached the Warehouse (or stored already Posted when nothing was consumed). The note is the reason.
    /// </summary>
    public static OperationReport CreateReversal(OperationReport original, string docNo, DateOnly postingDate, string note, string? idempotencyKey, string? requestHash, DateTime now, string by)
    {
        MfgRules.ThrowIf(string.IsNullOrWhiteSpace(docNo), MfgErrors.Gen002);
        MfgRules.ThrowIf(original.Status != OperationReportStatus.Posted || original.IsReversal || original.IsReversed, MfgErrors.Rev001, ("document", original.DocNo), ("reason", "chỉ đảo được báo cáo đã ghi nhận và chưa bị đảo"));
        return new OperationReport
        {
            Id = Guid.NewGuid(), DocNo = docNo, PlantId = original.PlantId, OrderId = original.OrderId, OrderNo = original.OrderNo, OrderOperationId = original.OrderOperationId, OperationSeq = original.OperationSeq,
            ReportedAt = now, PostingDate = postingDate, GoodQty = original.GoodQty, ScrapQty = original.ScrapQty, ReworkQty = original.ReworkQty, ScrapReasonId = original.ScrapReasonId,
            ReworkReasonId = original.ReworkReasonId, MachineId = original.MachineId, ReportedBy = by, Note = note.Trim(), ReversalOfId = original.Id, IdempotencyKey = idempotencyKey, RequestHash = requestHash,
            CreatedBy = by, UpdatedBy = by,
        };
    }

    /// <summary>A Posted reversal has undone this report (the caller applies the reversal to the order in the same save).</summary>
    public void MarkReversed(string by)
    {
        MfgRules.ThrowIf(Status != OperationReportStatus.Posted || IsReversal || IsReversed, MfgErrors.Rev001, ("document", DocNo), ("reason", "báo cáo đã bị đảo"));
        IsReversed = true;
        UpdatedBy = by;
        Touch();
    }

    /// <summary>The report waits for this backflush document.</summary>
    public void WaitFor(Guid backflushDocumentId)
    {
        BackflushDocumentId = backflushDocumentId;
        Touch();
    }

    /// <summary>The order has taken the report's figures (the caller applies them to the order in the same save).</summary>
    public void MarkPosted(DateTime now, string by)
    {
        MfgRules.ThrowIf(Status != OperationReportStatus.Pending, MfgErrors.Gen001);
        Status = OperationReportStatus.Posted;
        PostedAt = now;
        FailureCode = null;
        FailureMessage = null;
        UpdatedBy = by;
        Touch();
    }

    /// <summary>The backflush was refused (or abandoned): the report is kept for audit and never applied.</summary>
    public void MarkFailed(string code, string message, string by)
    {
        MfgRules.ThrowIf(Status != OperationReportStatus.Pending, MfgErrors.Gen001);
        Status = OperationReportStatus.Failed;
        FailureCode = code.Length > 60 ? code[..60] : code;
        FailureMessage = message.Length > 500 ? message[..500] : message;
        UpdatedBy = by;
        Touch();
    }

    /// <summary>The event that tells the world a reversal report undid <paramref name="original"/>.</summary>
    internal OperationReportReversedEvent ToReversedEvent(OperationReport original) => new()
    {
        ReversalReportId = Id, OriginalReportId = original.Id, DocNo = DocNo, OriginalDocNo = original.DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, OperationSeq = OperationSeq,
        GoodQty = GoodQty, ScrapQty = ScrapQty, ReworkQty = ReworkQty, ReversedBy = ReportedBy, Note = Note,
    };

    /// <summary>The event that tells the world the report counts. Raised by the order when it applies the report.</summary>
    internal OperationReportedEvent ToEvent() => new()
    {
        ReportId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, OperationSeq = OperationSeq, GoodQty = GoodQty, ScrapQty = ScrapQty,
        ReworkQty = ReworkQty, ScrapReasonId = ScrapReasonId, ReworkReasonId = ReworkReasonId, MachineId = MachineId, ReportedBy = ReportedBy,
    };

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
