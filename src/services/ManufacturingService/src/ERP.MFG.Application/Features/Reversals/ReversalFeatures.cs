using System.Security.Cryptography;
using System.Text;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Operations;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Reversals;

// ── Reversal of an operation report (BR-OP-07) and of a finished-goods receipt (BR-FG-07), P4-2 ─────────────────
// A reversal is a counter-document, never an edit in place. It is checked in full BEFORE anything is stored (a violation is a plain 400 and no document), then stored and sent to the
// Warehouse on the pending-document engine (Warehouse down = 202 Pending, finished by the retry job exactly once; a refusal rolls the reversal back and fails it).

internal static class ReversalGuards
{
    public const int MinNoteLength = 3;

    /// <summary>The reason of the reversal: a few words typed by the person, kept on the reversal document (not a reason-code type).</summary>
    public static string RequireNote(string? note)
    {
        var text = note?.Trim() ?? string.Empty;
        if (text.Length < MinNoteLength) throw MfgRules.Invalid("Vui lòng nhập lý do đảo chứng từ (ít nhất 3 ký tự).");
        return text.Length > 500 ? text[..500] : text;
    }

    /// <summary>While a reversal of the order waits for the Warehouse, new reports, receipts and reversals wait too: their backflush figures depend on the consumption it is about to change.</summary>
    public static async Task EnsureNoPendingReversalAsync(IStockDocumentRepository documents, IOperationReportRepository reports, Guid orderId, CancellationToken ct)
    {
        var waiting = await documents.CountPendingReversalsAsync(orderId, ct) + (await reports.ListPendingReversalsForOrderAsync(orderId, ct)).Count;
        MfgRules.ThrowIf(waiting > 0, MfgErrors.Rev002);
    }

    public static string Hash(string kind, Guid orderId, Guid originalId, string note)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}|{orderId}|{originalId}|{note}")));
}

// ── Operation report ────────────────────────────────────────────────────────────────────────────────────────────

public sealed record ReverseOperationReportCommand(Guid OrderId, Guid ReportId, string? Note, string? IdempotencyKey) : IRequest<OperationReportResultDto>;

public sealed class ReverseOperationReportHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IOperationReportRepository reports, IStockDocumentRepository documents, IProductionSettingProvider settings,
    IDocumentNumberService numbers, IPlantClock clock, IStockPostingService posting, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<ReverseOperationReportCommand, OperationReportResultDto>
{
    public async Task<OperationReportResultDto> Handle(ReverseOperationReportCommand r, CancellationToken ct)
    {
        var note = ReversalGuards.RequireNote(r.Note);
        var hash = ReversalGuards.Hash("ReverseReport", r.OrderId, r.ReportId, note);
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var repeat = await FindRepeatAsync(r.IdempotencyKey, hash, order.Id, ct);
        if (repeat is not null) return await AnswerAsync(repeat, ct);

        order.EnsureOpenForWork();                                                  // Closed / Cancelled final, OnHold MFG_PO_011, Completed or Draft MFG_OP_001 (reopen the order first)
        var original = await reports.GetAsync(r.ReportId, ct);
        if (original is null || original.OrderId != order.Id) throw new EntityNotFoundException(nameof(OperationReport), r.ReportId);
        MfgRules.ThrowIf(original.Status != OperationReportStatus.Posted || original.IsReversal || original.IsReversed, MfgErrors.Rev001, ("document", original.DocNo),
            ("reason", original.IsReversal ? "đây là phiếu đảo" : original.IsReversed ? "báo cáo đã bị đảo" : "báo cáo chưa được ghi nhận"));
        var earlier = await reports.FindReversalOfAsync(original.Id, ct);
        MfgRules.ThrowIf(earlier is not null, MfgErrors.Rev001, ("document", original.DocNo), ("reason", $"đã có phiếu đảo {earlier?.DocNo}"));
        await ReversalGuards.EnsureNoPendingReversalAsync(documents, reports, order.Id, ct);

        // BR-OP-07: what is left on the operation must still cover what the next operation (or the receipts) took from it
        var op = order.OperationOf(original.OperationSeq);
        if (original.GoodQty > 0)
        {
            var pending = await reports.ListPendingForOrderAsync(order.Id, ct);
            var next = order.Operations.FirstOrDefault(o => o.OperationSeq > op.OperationSeq);
            decimal used;
            if (next is not null) used = next.TakenQty + pending.Where(p => p.OperationSeq == next.OperationSeq).Sum(p => p.TotalQty);
            else
            {
                var flows = await documents.FlowsAsync(order.Id, ct);
                used = order.CompletedQty + flows.Where(f => f.Kind == StockLineKind.Receipt && f.OrderOutputId == null && f.Type == StockDocumentType.Receipt
                    && f.DocumentStatus == StockDocumentStatus.Pending && f.DocumentMode == StockDocumentMode.Forward).Sum(f => f.Qty);
            }
            var (covered, remaining, taken) = ReversalChecks.ReportCovered(op.GoodQty, original.GoodQty, used);
            MfgRules.ThrowIf(!covered, MfgErrors.Op007, ("detail", $" Còn lại {remaining:0.######}, đã dùng {taken:0.######}."));
        }

        // the backflush the report caused comes back with it
        MaterialTransaction? backflush = null;
        if (original.BackflushDocumentId is { } backflushId)
        {
            backflush = await documents.GetAsync(backflushId, ct) ?? throw new EntityNotFoundException(nameof(MaterialTransaction), backflushId);
            MfgRules.ThrowIf(backflush.Status != StockDocumentStatus.Posted || backflush.IsReversed, MfgErrors.Rev001, ("document", backflush.DocNo), ("reason", "chứng từ trừ vật tư tự động không ở trạng thái đã ghi nhận"));
        }
        var lines = backflush is null ? [] : ReversalPlanner.ReturnMaterial(backflush);

        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var by = user.GetUserId();
        var now = clock.UtcNow;
        var today = clock.Today(plant);
        var reportNo = await numbers.NextAsync(DocumentTypes.OperationReport, plant.Id, today.ToDateTime(TimeOnly.MinValue), ct);
        var reversal = OperationReport.CreateReversal(original, reportNo, today, note, r.IdempotencyKey, hash, now, by);

        if (lines.Count == 0)
        {
            // nothing was consumed: nothing to give back, the reversal counts at once in the same save that stores it
            var setting = await settings.GetAsync(plant.Id, ct);
            order.ApplyOperationReportReversal(original, reversal, setting.RequireSequentialOperations, by, now);
            original.MarkReversed(by);
            reversal.MarkPosted(now, by);
            reports.Add(reversal);
            reversal = await StoreAsync(reversal, hash, order.Id, ct);
            return await AnswerAsync(reversal, ct);
        }

        var docNo = await numbers.NextAsync(DocumentTypes.Reversal, Guid.Empty, today.ToDateTime(TimeOnly.MinValue), ct);   // per tenant: the number is the Warehouse key
        var document = MaterialTransaction.Create(docNo, StockDocumentType.Reversal, plant.Id, order.Id, order.OrderNo, today, null, $"Reversal of {original.DocNo}: {note}", null, null, lines, by,
            reversal.Id, backflush!.Id);
        document.Claim(now + MaterialDocuments.FirstAttemptDelay);                  // if this request dies, the job takes the document up after a short while
        reversal.WaitFor(document.Id);
        reports.Add(reversal);
        documents.Add(document);
        reversal = await StoreAsync(reversal, hash, order.Id, ct);
        if (reversal.BackflushDocumentId != document.Id) return await AnswerAsync(reversal, ct);   // a parallel request with the same key won: its document is the one

        await posting.PostAsync(document, by, ct);
        return await AnswerAsync(reversal, ct);
    }

    private async Task<OperationReport?> FindRepeatAsync(string? key, string hash, Guid orderId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(key)) return null;
        MfgRules.ThrowIf(key.Length > MaterialDocuments.MaxKeyLength, MfgErrors.Gen007);
        var existing = await reports.FindByKeyAsync(key, ct);
        if (existing is null) return null;
        MfgRules.ThrowIf(existing.RequestHash != hash || existing.OrderId != orderId, MfgErrors.Gen007);
        return existing;
    }

    private async Task<OperationReport> StoreAsync(OperationReport report, string hash, Guid orderId, CancellationToken ct)
    {
        try
        {
            await uow.SaveChangesAsync(ct);
            return report;
        }
        catch (ConcurrencyException) when (!string.IsNullOrEmpty(report.IdempotencyKey))
        {
            return await FindRepeatAsync(report.IdempotencyKey, hash, orderId, ct) ?? throw new ConcurrencyException(MfgErrors.Format(MfgErrors.Gen001));
        }
    }

    /// <summary>201 when the reversal counts, 202 while its giving back of material is on its way to the Warehouse, 400 with the reason when it failed.</summary>
    private async Task<OperationReportResultDto> AnswerAsync(OperationReport report, CancellationToken ct)
    {
        var document = report.BackflushDocumentId is { } id ? await documents.GetAsync(id, ct) : null;
        if (report.Status == OperationReportStatus.Failed)
            throw new BusinessRuleException(report.FailureCode ?? MfgErrors.Fg004, report.FailureMessage ?? "The reversal was refused.");
        return new OperationReportResultDto(OperationReportDto.From(report), report.Status == OperationReportStatus.Pending, document is null ? null : StockDocumentDto.From(document));
    }
}

// ── Finished-goods receipt ──────────────────────────────────────────────────────────────────────────────────────

public sealed record ReverseReceiptCommand(Guid OrderId, Guid DocumentId, string? Note, string? IdempotencyKey) : IRequest<StockDocumentResultDto>;

public sealed class ReverseReceiptHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IStockDocumentRepository documents, IOperationReportRepository reports, IWarehouseStockClient stock,
    IDocumentNumberService numbers, IPlantClock clock, IStockPostingService posting, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<ReverseReceiptCommand, StockDocumentResultDto>
{
    public async Task<StockDocumentResultDto> Handle(ReverseReceiptCommand r, CancellationToken ct)
    {
        var note = ReversalGuards.RequireNote(r.Note);
        var hash = ReversalGuards.Hash("ReverseReceipt", r.OrderId, r.DocumentId, note);
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var repeat = await MaterialDocuments.FindRepeatAsync(documents, r.IdempotencyKey, hash, order.Id, ct);
        if (repeat is not null) return new StockDocumentResultDto(StockDocumentDto.From(repeat), repeat.Status == StockDocumentStatus.Pending);

        order.EnsureEditable();                                                     // Closed / Cancelled are final
        MfgRules.ThrowIf(order.Status == OrderStatus.OnHold, MfgErrors.Po011);
        MfgRules.ThrowIf(order.Status is not (OrderStatus.Released or OrderStatus.InProgress or OrderStatus.Completed), MfgErrors.Fg005);
        var original = await documents.GetAsync(r.DocumentId, ct);
        if (original is null || original.OrderId != order.Id) throw new EntityNotFoundException(nameof(MaterialTransaction), r.DocumentId);
        MfgRules.ThrowIf(original.Type != StockDocumentType.Receipt || original.Status != StockDocumentStatus.Posted || original.IsReversed, MfgErrors.Rev001, ("document", original.DocNo),
            ("reason", original.IsReversed ? "phiếu đã bị đảo" : original.Type != StockDocumentType.Receipt ? "đây không phải phiếu nhập kho thành phẩm" : "phiếu chưa được ghi nhận"));
        var earlier = await documents.FindReversalOfAsync(original.Id, ct);
        MfgRules.ThrowIf(earlier is not null, MfgErrors.Rev001, ("document", original.DocNo), ("reason", $"đã có phiếu đảo {earlier?.DocNo}"));
        await ReversalGuards.EnsureNoPendingReversalAsync(documents, reports, order.Id, ct);

        // BR-FG-07: a lot released from quality hold counts as inspected (until the FQC result of P4-3 replaces this check) ...
        var received = original.ReceiptLines.Where(l => l.Status == StockLineStatus.Posted).ToList();
        foreach (var line in received.Where(l => l.QcReleasedAt is not null))
            throw MfgRules.Fail(MfgErrors.Fg004, ("reason", $"lô {line.LotNumber ?? line.ItemCode} đã được giải phóng khỏi kho chờ kiểm"));

        // ... and every lot must still be whole and free in the Warehouse (nothing sold, issued, consumed or reserved)
        foreach (var line in received)
        {
            var availability = await stock.GetAvailabilityAsync(line.ItemId, line.WarehouseId, ct);      // Warehouse down: MFG_INT_001, nothing is stored
            var row = availability.Balances.FirstOrDefault(b => b.WarehouseId == line.WarehouseId && b.LotId == line.PostedLotId);
            var problem = ReversalChecks.LotProblem(line.LotNumber ?? "(không lô)", line.Qty, row?.Quantity ?? 0m, row?.ReservedQuantity ?? 0m);
            if (problem is not null) throw MfgRules.Fail(MfgErrors.Fg004, ("reason", problem));
        }

        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var by = user.GetUserId();
        var now = clock.UtcNow;
        var today = clock.Today(plant);
        // the finished goods leave first (the step most likely to be refused), then the backflush part comes back
        var lines = ReversalPlanner.TakeOutReceipt(original).Concat(ReversalPlanner.ReturnMaterial(original)).ToList();
        var docNo = await numbers.NextAsync(DocumentTypes.Reversal, Guid.Empty, today.ToDateTime(TimeOnly.MinValue), ct);
        var document = MaterialTransaction.Create(docNo, StockDocumentType.Reversal, plant.Id, order.Id, order.OrderNo, today, null, $"Reversal of {original.DocNo}: {note}", r.IdempotencyKey, hash, lines, by,
            null, original.Id);
        document.Claim(now + MaterialDocuments.FirstAttemptDelay);
        document = await MaterialDocuments.StoreAsync(documents, uow, document, r.IdempotencyKey, hash, order.Id, ct);
        if (document.Status != StockDocumentStatus.Pending || document.Attempts > 0 || document.PostedLines.Any())
            return new StockDocumentResultDto(StockDocumentDto.From(document), document.Status == StockDocumentStatus.Pending);

        return MaterialDocuments.Conclude(document, await posting.PostAsync(document, by, ct));
    }
}
