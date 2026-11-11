using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>A line to store: ONE Warehouse movement of one material of the order.</summary>
public sealed record NewStockLine(Guid? OrderMaterialId, int? MaterialLineNo, Guid ItemId, string ItemCode, Guid WarehouseId, Guid? LotId, decimal Qty, bool IsFromReservation, decimal? UnitCost, bool IsNegativeStock = false,
    StockLineKind Kind = StockLineKind.Material, Guid? OrderOutputId = null, int? OutputLineNo = null, string? LotNumber = null, DateTime? ExpiryDate = null, DateTime? ManufactureDate = null, bool IsQcHold = false,
    ReversalAction? Action = null, Guid? ReversesLineId = null)
{
    /// <summary>A line that brings the main product (<paramref name="outputId"/> null) or a co- / by-product into stock (P3-5). The lot is created by the Warehouse from <paramref name="lotNumber"/>.</summary>
    public static NewStockLine Receipt(Guid itemId, string itemCode, Guid warehouseId, decimal qty, Guid? outputId, int? outputLineNo, string? lotNumber, DateTime? expiry, DateTime? manufactured, bool qcHold)
        => new(null, null, itemId, itemCode, warehouseId, null, qty, false, null, false, StockLineKind.Receipt, outputId, outputLineNo, lotNumber, expiry, manufactured, qcHold);
}

/// <summary>One lot a Warehouse movement really used (Warehouse W7 <c>lots[]</c>), kept on the line so a reversal can name the lot(s) it gives back.</summary>
public sealed record LineLot(Guid LotId, string LotNumber, decimal Qty, decimal UnitCost, Guid? WarehouseId, Guid? ParentLotId);

/// <summary>A stored <see cref="LineLot"/> row of a line (P4-2): the per-lot breakdown of the movement, in the order the Warehouse answered it.</summary>
public class MaterialTransactionLineLot : BaseEntity
{
    private MaterialTransactionLineLot() { }

    public Guid LineId { get; private set; }
    public int Position { get; private set; }
    public Guid LotId { get; private set; }
    public string LotNumber { get; private set; } = string.Empty;
    public decimal Qty { get; private set; }
    public decimal? UnitCost { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? ParentLotId { get; private set; }

    internal static MaterialTransactionLineLot Create(Guid lineId, int position, LineLot v) => new()
    {
        Id = Guid.NewGuid(), LineId = lineId, Position = position, LotId = v.LotId, LotNumber = v.LotNumber, Qty = v.Qty, UnitCost = v.UnitCost, WarehouseId = v.WarehouseId, ParentLotId = v.ParentLotId,
    };
}

/// <summary>
/// One line of a stock document = one movement in the Warehouse. Its key (<c>&lt;DocNo&gt;#&lt;n&gt;</c>; a free-stock issue ends with <c>F</c>, the reversal of
/// a posted line with <c>R</c>) is what the Warehouse deduplicates on, so a line can be sent again after any failure without moving stock twice.
/// </summary>
public class MaterialTransactionLine : BaseEntity
{
    private MaterialTransactionLine() { }

    public Guid TransactionId { get; private set; }
    public int LineNo { get; private set; }
    /// <summary>The material of the order this line moves; null on a receipt line.</summary>
    public Guid? OrderMaterialId { get; private set; }
    public int? MaterialLineNo { get; private set; }
    public StockLineKind Kind { get; private set; } = StockLineKind.Material;

    /// <summary>Receipt line: the co- / by-product of the order it brings in (null = the main product).</summary>
    public Guid? OrderOutputId { get; private set; }
    public int? OutputLineNo { get; private set; }

    /// <summary>Receipt line: the lot the Warehouse creates, its dates, and whether it starts on quality hold (BR-FG-02, BR-FG-04).</summary>
    public string? LotNumber { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public DateTime? ManufactureDate { get; private set; }
    public bool IsQcHold { get; private set; }

    /// <summary>When the interim quality release moved the held lot to the finished-goods warehouse and made it Available.</summary>
    public DateTime? QcReleasedAt { get; private set; }

    /// <summary>Reversal documents only (P4-2): what this line does in the Warehouse, and the line of the original document it undoes.</summary>
    public ReversalAction? ReversalAction { get; private set; }

    public Guid? ReversesLineId { get; private set; }

    private readonly List<MaterialTransactionLineLot> _lots = [];

    /// <summary>The lots the Warehouse really used for this line (W7), in answer order; empty for a line posted before they were stored or a lot-less line.</summary>
    public IReadOnlyList<MaterialTransactionLineLot> Lots => _lots.Where(l => !l.IsDeleted).OrderBy(l => l.Position).ToList();

    public IReadOnlyList<MaterialTransactionLineLot> AllLots => _lots;

    public Guid ItemId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public Guid WarehouseId { get; private set; }
    public Guid? LotId { get; private set; }
    public decimal Qty { get; private set; }

    /// <summary>An issue taken from the order's Warehouse reservation (consumes it) rather than from free stock.</summary>
    public bool IsFromReservation { get; private set; }

    /// <summary>A backflush line that takes stock below zero (allowed by <c>AllowNegativeStockOnBackflush</c>, BR-MAT-08): sent with <c>AllowNegative</c>.</summary>
    public bool IsNegativeStock { get; private set; }

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
        WarehouseId = v.WarehouseId, LotId = v.LotId, Qty = v.Qty, IsFromReservation = v.IsFromReservation, UnitCost = v.UnitCost, IsNegativeStock = v.IsNegativeStock,
        Kind = v.Kind, OrderOutputId = v.OrderOutputId, OutputLineNo = v.OutputLineNo, LotNumber = v.LotNumber, ExpiryDate = v.ExpiryDate, ManufactureDate = v.ManufactureDate, IsQcHold = v.IsQcHold, ReversalAction = v.Action, ReversesLineId = v.ReversesLineId,
        MovementKey = KeyFor(docNo, lineNo, v.Kind == StockLineKind.Material && type is not (StockDocumentType.Return or StockDocumentType.Reversal) && !v.IsFromReservation),
    };

    internal static string KeyFor(string docNo, int lineNo, bool free) => $"{docNo}#{lineNo}{(free ? "F" : "")}";

    internal void MarkPosted(Guid movementId, decimal? unitCost, decimal? valueDelta, Guid? lotId, IReadOnlyList<LineLot>? lots = null)
    {
        if (lots is { Count: > 0 } && _lots.Count == 0)
            for (var i = 0; i < lots.Count; i++) _lots.Add(MaterialTransactionLineLot.Create(Id, i, lots[i]));
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

    internal void MarkQcReleased(DateTime now)
    {
        QcReleasedAt = now;
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

    /// <summary>The document has been undone by a Posted reversal (P4-2): its figures are no longer in the order, its stock is no longer in the Warehouse.</summary>
    public bool IsReversed { get; private set; }

    /// <summary>For a backflush: the operation report whose output it consumes for (applied together with it).</summary>
    public Guid? SourceOperationReportId { get; private set; }

    public IReadOnlyList<MaterialTransactionLine> Lines => _lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNo).ToList();

    /// <summary>Persistence access to the loaded lines (EF fills the field).</summary>
    public IReadOnlyList<MaterialTransactionLine> AllLines => _lines;

    /// <summary>The finished-goods lines of a receipt (empty on every other document).</summary>
    public IEnumerable<MaterialTransactionLine> ReceiptLines => Lines.Where(l => l.Kind == StockLineKind.Receipt);

    /// <summary>The material lines: what an issue, a return or a backflush moves, and the backflush part of a receipt.</summary>
    public IEnumerable<MaterialTransactionLine> MaterialLines => Lines.Where(l => l.Kind == StockLineKind.Material);

    /// <summary>True when the line takes stock out (an issue, a backflush, or the backflush part of a receipt; on a reversal: a lot leaving stock) rather than bringing it in (a return, a receipt line, a reversal giving stock back).</summary>
    public bool TakesStockOut(MaterialTransactionLine line)
        => Type == StockDocumentType.Reversal ? line.ReversalAction is ReversalAction.Scrap or ReversalAction.Issue : line.Kind == StockLineKind.Material && Type != StockDocumentType.Return;

    public bool IsReversal => Type == StockDocumentType.Reversal;

    public bool IsIssue => Type == StockDocumentType.Issue;

    /// <summary>Stock leaves the warehouse: an issue or a backflush (a return brings it back).</summary>
    public bool IsConsumption => Type is StockDocumentType.Issue or StockDocumentType.Backflush;

    public static MaterialTransaction Create(string docNo, StockDocumentType type, Guid plantId, Guid orderId, string orderNo, DateOnly postingDate, Guid? reasonCodeId, string? note,
        string? idempotencyKey, string? requestHash, IReadOnlyList<NewStockLine> lines, string createdBy, Guid? sourceOperationReportId = null, Guid? reversalOfId = null)
    {
        MfgRules.ThrowIf(string.IsNullOrWhiteSpace(docNo), MfgErrors.Gen002);
        MfgRules.ThrowIf(lines.Count == 0 || lines.Any(l => l.Qty <= 0), MfgErrors.Mat001);
        var doc = new MaterialTransaction
        {
            Id = Guid.NewGuid(), DocNo = docNo, Type = type, PlantId = plantId, OrderId = orderId, OrderNo = orderNo, PostingDate = postingDate, ReasonCodeId = reasonCodeId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), IdempotencyKey = idempotencyKey, RequestHash = requestHash, SourceOperationReportId = sourceOperationReportId, ReversalOfId = reversalOfId, CreatedBy = createdBy, UpdatedBy = createdBy,
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

    public void RecordLinePosted(MaterialTransactionLine line, Guid movementId, decimal? unitCost, decimal? valueDelta, Guid? lotId, IReadOnlyList<LineLot>? lots = null)
    {
        line.MarkPosted(movementId, unitCost, valueDelta, lotId, lots);
        Attempts = 0;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConvertLineToFree(MaterialTransactionLine line) => line.ConvertToFree();

    /// <summary>The interim quality release moved the line's lot to the finished-goods warehouse and made it Available.</summary>
    public void RecordQcReleased(MaterialTransactionLine line, DateTime now)
    {
        line.MarkQcReleased(now);
        UpdatedAt = DateTime.UtcNow;
    }

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
        if (Type == StockDocumentType.Reversal)
        {
            var receipt = ReceiptLines.Where(l => l.Status == StockLineStatus.Posted).ToList();
            if (receipt.Count > 0)
                AddDomainEvent(new FinishedGoodsReceiptReversedEvent
                {
                    DocumentId = Id, DocNo = DocNo, ReversalOfId = ReversalOfId ?? Guid.Empty, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate,
                    Lines = receipt.Select(l => new ReceiptMovementLine
                    {
                        ItemId = l.ItemId, ItemCode = l.ItemCode, WarehouseId = l.WarehouseId, LotId = l.PostedLotId ?? l.LotId, Quantity = l.Qty, UnitCost = l.UnitCost, Amount = l.Amount,
                        OutputLineNo = l.OutputLineNo, LotNumber = l.LotNumber, ExpiryDate = l.ExpiryDate, IsQcHold = l.IsQcHold,
                    }).ToList(),
                });
            var returned = MaterialLines.Where(l => l.Status == StockLineStatus.Posted).ToList();
            if (returned.Count > 0)
                AddDomainEvent(new MaterialReturnedEvent
                {
                    DocumentId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate, ReasonCodeId = null,
                    Lines = returned.Select(l => new StockMovementLine { ItemId = l.ItemId, ItemCode = l.ItemCode, WarehouseId = l.WarehouseId, LotId = l.PostedLotId ?? l.LotId, Quantity = l.Qty, UnitCost = l.UnitCost, Amount = l.Amount }).ToList(),
                });
        }
        else if (Type == StockDocumentType.Receipt)
        {
            AddDomainEvent(new FinishedGoodsReceivedEvent
            {
                DocumentId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate,
                Lines = ReceiptLines.Where(l => l.Status == StockLineStatus.Posted).Select(l => new ReceiptMovementLine
                {
                    ItemId = l.ItemId, ItemCode = l.ItemCode, WarehouseId = l.WarehouseId, LotId = l.PostedLotId, Quantity = l.Qty, UnitCost = l.UnitCost, Amount = l.Amount,
                    OutputLineNo = l.OutputLineNo, LotNumber = l.LotNumber, ExpiryDate = l.ExpiryDate, IsQcHold = l.IsQcHold,
                }).ToList(),
            });
            var consumed = MaterialLines.Where(l => l.Status == StockLineStatus.Posted).ToList();
            if (consumed.Count > 0)
                AddDomainEvent(new MaterialBackflushedEvent
                {
                    DocumentId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate,
                    Lines = consumed.Select(l => new StockMovementLine { ItemId = l.ItemId, ItemCode = l.ItemCode, WarehouseId = l.WarehouseId, LotId = l.PostedLotId ?? l.LotId, Quantity = l.Qty, UnitCost = l.UnitCost, Amount = l.Amount }).ToList(),
                });
        }
        else if (Type == StockDocumentType.Backflush)
            AddDomainEvent(new MaterialBackflushedEvent { DocumentId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate, SourceOperationReportId = SourceOperationReportId, Lines = lines });
        else if (IsIssue)
            AddDomainEvent(new MaterialIssuedEvent { DocumentId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate, ReasonCodeId = ReasonCodeId, Lines = lines });
        else
            AddDomainEvent(new MaterialReturnedEvent { DocumentId = Id, DocNo = DocNo, OrderId = OrderId, OrderNo = OrderNo, PlantId = PlantId, PostingDate = PostingDate, ReasonCodeId = ReasonCodeId, Lines = lines });
    }

    /// <summary>A Posted reversal has undone this document (the caller applies the reversal to the order in the same save).</summary>
    public void MarkReversed(string by)
    {
        MfgRules.ThrowIf(Status != StockDocumentStatus.Posted || IsReversal, MfgErrors.Gen001);
        IsReversed = true;
        Touch(by);
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
