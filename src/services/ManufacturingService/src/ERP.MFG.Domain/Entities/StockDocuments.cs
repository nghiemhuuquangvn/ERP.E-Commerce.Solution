using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>A line to store: ONE Warehouse movement of one material of the order.</summary>
public sealed record NewStockLine(Guid OrderMaterialId, int MaterialLineNo, Guid ItemId, string ItemCode, Guid WarehouseId, Guid? LotId, decimal Qty, bool IsFromReservation, decimal? UnitCost);

/// <summary>
/// One line of a stock document = one movement in the Warehouse. Its key (<c>&lt;DocNo&gt;#&lt;n&gt;</c>; a free-stock issue ends with <c>F</c>, the reversal of
/// a posted line with <c>R</c>) is what the Warehouse deduplicates on, so a line can be sent again after any failure without moving stock twice.
/// </summary>
public class MaterialTransactionLine : BaseEntity
{
    private MaterialTransactionLine() { }

    public Guid TransactionId { get; private set; }
    public int LineNo { get; private set; }
    public Guid OrderMaterialId { get; private set; }
    public int MaterialLineNo { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public Guid WarehouseId { get; private set; }
    public Guid? LotId { get; private set; }
    public decimal Qty { get; private set; }

    /// <summary>An issue taken from the order's Warehouse reservation (consumes it) rather than from free stock.</summary>
    public bool IsFromReservation { get; private set; }

    public string MovementKey { get; private set; } = string.Empty;
    public StockLineStatus Status { get; private set; } = StockLineStatus.Pending;
    public Guid? WhMovementId { get; private set; }
    public decimal? UnitCost { get; private set; }
    public decimal? Amount { get; private set; }

    /// <summary>The lot the Warehouse actually booked (when one lot was used).</summary>
    public Guid? PostedLotId { get; private set; }

    public Guid? CompensationMovementId { get; private set; }
    public string? Error { get; private set; }

    /// <summary>The key of the reversing movement.</summary>
    public string CompensationKey => MovementKey + "R";

    internal static MaterialTransactionLine Create(Guid transactionId, string docNo, StockDocumentType type, int lineNo, NewStockLine v) => new()
    {
        Id = Guid.NewGuid(), TransactionId = transactionId, LineNo = lineNo, OrderMaterialId = v.OrderMaterialId, MaterialLineNo = v.MaterialLineNo, ItemId = v.ItemId, ItemCode = v.ItemCode,
        WarehouseId = v.WarehouseId, LotId = v.LotId, Qty = v.Qty, IsFromReservation = v.IsFromReservation, UnitCost = v.UnitCost,
        MovementKey = KeyFor(docNo, lineNo, type == StockDocumentType.Issue && !v.IsFromReservation),
    };

    internal static string KeyFor(string docNo, int lineNo, bool free) => $"{docNo}#{lineNo}{(free ? "F" : "")}";

    internal void MarkPosted(Guid movementId, decimal? unitCost, decimal? valueDelta, Guid? lotId)
    {
        Status = StockLineStatus.Posted;
        WhMovementId = movementId;
        UnitCost = unitCost ?? UnitCost;
        Amount = valueDelta is null ? null : Math.Abs(valueDelta.Value);
        PostedLotId = lotId;
        Error = null;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void MarkRefused(string error)
    {
        Status = StockLineStatus.Refused;
        Error = error.Length > 500 ? error[..500] : error;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void MarkCompensated(Guid movementId)
    {
        Status = StockLineStatus.Compensated;
        CompensationMovementId = movementId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>The reservation cannot cover this line any more: take it from free stock under the key of a free line. Only a line that was never booked.</summary>
    internal void ConvertToFree()
    {
        MfgRules.ThrowIf(Status != StockLineStatus.Pending || !IsFromReservation, MfgErrors.Gen001);
        IsFromReservation = false;
        MovementKey += "F";
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// A material issue or return (BA spec 01 §7.3). The document is stored first, then sent to the Warehouse line by line (see <c>StockPostingService</c>): the
/// effect on the production order is applied only when every line is posted, so the order never shows stock the Warehouse has not booked (ADR Phase 3, decision 3).
/// </summary>
public class MaterialTransaction : BaseEntity
{
    private readonly List<MaterialTransactionLine> _lines = [];

    private MaterialTransaction() { }

    public string DocNo { get; private set; } = string.Empty;
    public Guid PlantId { get; private set; }
    public Guid OrderId { get; private set; }
    public string OrderNo { get; private set; } = string.Empty;
    public StockDocumentType Type { get; private set; }
    public StockDocumentStatus Status { get; private set; } = StockDocumentStatus.Pending;
    public StockDocumentMode Mode { get; private set; } = StockDocumentMode.Forward;
    public DateOnly PostingDate { get; private set; }
    public Guid? ReasonCodeId { get; private set; }
    public string? Note { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? RequestHash { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public string? LastErrorCode { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public Guid? ReversalOfId { get; private set; }

    public IReadOnlyList<MaterialTransactionLine> Lines => _lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNo).ToList();

    /// <summary>Persistence access to the loaded lines (EF fills the field).</summary>
    public IReadOnlyList<MaterialTransactionLine> AllLines => _lines;

    public bool IsIssue => Type == StockDocumentType.Issue;

    public static MaterialTransaction Create(string docNo, StockDocumentType type, Guid plantId, Guid orderId, string orderNo, DateOnly postingDate, Guid? reasonCodeId, string? note,
        string? idempotencyKey, string? requestHash, IReadOnlyList<NewStockLine> lines, string createdBy)
    {
        MfgRules.ThrowIf(string.IsNullOrWhiteSpace(docNo), MfgErrors.Gen002);
        MfgRules.ThrowIf(lines.Count == 0 || lines.Any(l => l.Qty <= 0), MfgErrors.Mat001);
        var doc = new MaterialTransaction
        {
            Id = Guid.NewGuid(), DocNo = docNo, Type = type, PlantId = plantId, OrderId = orderId, OrderNo = orderNo, PostingDate = postingDate, ReasonCodeId = reasonCodeId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), IdempotencyKey = idempotencyKey, RequestHash = requestHash, CreatedBy = createdBy, UpdatedBy = createdBy,
        };
        var no = 0;
        foreach (var line in lines) doc._lines.Add(MaterialTransactionLine.Create(doc.Id, docNo, type, ++no, line));
        return doc;
    }

    // ── Progress while the lines go to the Warehouse ───────────────────────────────────────────────────────

    public IEnumerable<MaterialTransactionLine> PendingLines => Lines.Where(l => l.Status == StockLineStatus.Pending);
    public IEnumerable<MaterialTransactionLine> PostedLines => Lines.Where(l => l.Status == StockLineStatus.Posted);
    public bool AllLinesPosted => Lines.Count > 0 && Lines.All(l => l.Status == StockLineStatus.Posted);

    /// <summary>Nothing booked in the Warehouse is left to reverse.</summary>
    public bool RollbackComplete => Lines.All(l => l.Status != StockLineStatus.Posted);

    public void RecordLinePosted(MaterialTransactionLine line, Guid movementId, decimal? unitCost, decimal? valueDelta, Guid? lotId)
    {
        line.MarkPosted(movementId, unitCost, valueDelta, lotId);
        Attempts = 0;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConvertLineToFree(MaterialTransactionLine line) => line.ConvertToFree();

    /// <summary>The Warehouse could not be reached or answered a conflict: stay Pending, remember why, try again at <paramref name="next"/>. Never fails the document by counting attempts (a half-posted document would be inconsistent).</summary>
    public void RecordTransientFailure(string error, DateTime next)
    {
        Attempts++;
        LastError = Trim(error);
        NextAttemptAt = next;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>A line was refused: the lines not sent are dropped, and the lines already posted are reversed (<see cref="StockDocumentMode.Rollback"/>).</summary>
    public void RefuseLine(MaterialTransactionLine line, string code, string message)
    {
        line.MarkRefused(message);
        BeginRollback(code, message);
    }

    public void BeginRollback(string code, string message)
    {
        foreach (var pending in PendingLines.ToList()) pending.MarkRefused("Not sent: " + Trim(message));
        Mode = StockDocumentMode.Rollback;
        LastErrorCode = code.Length > 60 ? code[..60] : code;
        LastError = Trim(message);
        Attempts = 0;
        NextAttemptAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLineCompensated(MaterialTransactionLine line, Guid movementId)
    {
        line.MarkCompensated(movementId);
        Attempts = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>The reversal is done (or nothing was booked): the document is Failed for good.</summary>
    public void MarkFailed(string by)
    {
        MfgRules.ThrowIf(!RollbackComplete, MfgErrors.Gen001);
        Status = StockDocumentStatus.Failed;
        NextAttemptAt = null;
        Touch(by);
    }

    /// <summary>Every line is posted and the order has taken the effect: the document is Posted and its event leaves with the same save.</summary>
    public void MarkPosted(string by, DateTime now)
    {
        MfgRules.ThrowIf(!AllLinesPosted || Mode != StockDocumentMode.Forward, MfgErrors.Gen001);
        Status = StockDocumentStatus.Posted;
        PostedAt = now;
        NextAttemptAt = null;
        LastError = null;
        LastErrorCode = null;
        Touch(by);
        var lines = PostedLines.Select(l => new StockMovementLine
        {
            ItemId = l.ItemId, ItemCode = l.ItemCode, WarehouseId = l.WarehouseId, LotId = l.PostedLotId ?? l.LotId, Quantity = l.Qty, UnitCost = l.UnitCost, Amount = l.Amount,
        }).ToList();
        if (IsIssue)
            AddDomainEvent(new MaterialIssuedEvent { DocumentId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate, ReasonCodeId = ReasonCodeId, Lines = lines });
        else
            AddDomainEvent(new MaterialReturnedEvent { DocumentId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate, ReasonCodeId = ReasonCodeId, Lines = lines });
    }

    /// <summary>The user gives the document up: what is posted is reversed, then the document is Failed (<c>abandoned</c>).</summary>
    public void Abandon(string by)
    {
        MfgRules.ThrowIf(Status != StockDocumentStatus.Pending, MfgErrors.Gen001);
        BeginRollback("ABANDONED", $"Abandoned by {by}");
        Touch(by);
    }

    /// <summary>Manual retry: the next round of the job picks the document up at once.</summary>
    public void RetryNow(DateTime now)
    {
        MfgRules.ThrowIf(Status != StockDocumentStatus.Pending, MfgErrors.Gen001);
        NextAttemptAt = now;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>The job takes the document for a while: a second instance (or the request itself) sees a later time and leaves it alone.</summary>
    public void Claim(DateTime until)
    {
        NextAttemptAt = until;
        UpdatedAt = DateTime.UtcNow;
    }

    private void Touch(string by) { UpdatedBy = by; UpdatedAt = DateTime.UtcNow; }

    private static string Trim(string text) => text.Length > 500 ? text[..500] : text;
}

/// <summary>Back-off between two attempts to send a pending document: 10 s, 20 s, 40 s ... up to 5 minutes.</summary>
public static class StockRetryPolicy
{
    public static readonly TimeSpan First = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan Max = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    public static TimeSpan Backoff(int attempts)
        => TimeSpan.FromSeconds(Math.Min(First.TotalSeconds * Math.Pow(2, Math.Clamp(attempts, 0, 10)), Max.TotalSeconds));
}
