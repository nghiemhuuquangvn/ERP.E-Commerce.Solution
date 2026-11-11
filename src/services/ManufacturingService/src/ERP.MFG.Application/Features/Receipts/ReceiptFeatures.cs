using System.Security.Cryptography;
using System.Text;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Features.Reversals;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Receipts;

// ── Receive finished goods (BR-FG-01..06, BR-PO-17, BR-OP-09, BR-MAT-08) ──────────────────────────────────────

/// <summary>One line of a receipt: <c>OutputLineNo</c> null = the main product, else a co- / by-product line of the order; <c>LotNumber</c> optional (else generated).</summary>
public sealed record ReceiptLineBody(int? OutputLineNo, decimal Quantity, string? LotNumber);

public sealed record ReceiveFinishedGoodsCommand(Guid OrderId, IReadOnlyList<ReceiptLineBody> Lines, string? Note, string? IdempotencyKey) : IRequest<StockDocumentResultDto>;

public sealed class ReceiveFinishedGoodsHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IStockDocumentRepository documents, IOperationReportRepository reports, IWarehouseStockClient stock, IProductLookupClient products,
    IUnitOfMeasureClient units, IItemProfileRepository profiles, IProductionSettingProvider settings, BackflushPlanner planner, IDocumentNumberService numbers, IPlantClock clock,
    IStockPostingService posting, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<ReceiveFinishedGoodsCommand, StockDocumentResultDto>
{
    public async Task<StockDocumentResultDto> Handle(ReceiveFinishedGoodsCommand r, CancellationToken ct)
    {
        var lines = r.Lines ?? [];
        MfgRules.ThrowIf(lines.Count == 0 || lines.Any(l => l.Quantity <= 0), MfgErrors.Mat001);
        var hash = Hash(r.OrderId, lines, r.Note);
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var repeat = await MaterialDocuments.FindRepeatAsync(documents, r.IdempotencyKey, hash, order.Id, ct);
        if (repeat is not null) return new StockDocumentResultDto(StockDocumentDto.From(repeat), repeat.Status == StockDocumentStatus.Pending);

        order.EnsureEditable();
        MfgRules.ThrowIf(order.Status == OrderStatus.OnHold, MfgErrors.Po011);
        MfgRules.ThrowIf(order.Status is not (OrderStatus.Released or OrderStatus.InProgress), MfgErrors.Fg005);
        await ReversalGuards.EnsureNoPendingReversalAsync(documents, reports, order.Id, ct);   // P4-2: a reversal of this order is waiting for the Warehouse (MFG_REV_002)
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var setting = await settings.GetAsync(plant.Id, ct);
        var by = user.GetUserId();
        var now = clock.UtcNow;
        var today = clock.Today(plant);
        var manufactured = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        // what is received: the main product (one line) and co- / by-products (one line each, per lot number)
        var wanted = lines.GroupBy(l => (l.OutputLineNo, Lot: string.IsNullOrWhiteSpace(l.LotNumber) ? null : l.LotNumber.Trim().ToUpperInvariant()))
            .Select(g => (g.Key.OutputLineNo, g.Key.Lot, Qty: g.Sum(x => x.Quantity), Given: g.First().LotNumber?.Trim())).ToList();
        var outputs = wanted.Where(w => w.OutputLineNo is not null)
            .ToDictionary(w => w.OutputLineNo!.Value, w => order.Outputs.FirstOrDefault(o => o.LineNo == w.OutputLineNo) ?? throw MfgRules.Invalid($"Order {order.OrderNo} has no output line {w.OutputLineNo}."));

        var itemIds = new[] { order.ItemId }.Concat(outputs.Values.Select(o => o.ItemId)).Distinct().ToList();
        var known = (await products.LookupAsync(itemIds, ct)).ToDictionary(p => p.Id);
        foreach (var id in itemIds)
        {
            var item = known.GetValueOrDefault(id) ?? throw MfgRules.Invalid("An item of the receipt does not exist any more.");
            MfgRules.ThrowIf(item.IsSerialControlled, MfgErrors.Mat011, ("item", item.Sku));
        }

        // decimals of the units
        var unitIds = new[] { order.UomId }.Concat(outputs.Values.Select(o => o.UomId)).Distinct().ToList();
        var decimals = (await units.LookupAsync(unitIds, ct)).ToDictionary(u => u.Id, u => u.DecimalPlaces);
        foreach (var w in wanted)
        {
            var uom = w.OutputLineNo is null ? order.UomId : outputs[w.OutputLineNo.Value].UomId;
            var places = decimals.GetValueOrDefault(uom, 6);
            if (Math.Round(w.Qty, places) != w.Qty) throw MfgRules.Invalid($"The quantity can have at most {places} decimal places.");
        }

        // BR-FG-01 / BR-PO-17 / BR-OP-09 / BR-FG-06: what may still be received, counting receipts on their way
        var flows = await documents.FlowsAsync(order.Id, ct);
        decimal PendingReceipt(Guid? outputId) => flows.Where(f => f.Kind == StockLineKind.Receipt && f.OrderOutputId == outputId && f.DocumentStatus == StockDocumentStatus.Pending
            && f.DocumentMode == StockDocumentMode.Forward).Sum(f => f.Qty);
        foreach (var w in wanted)
        {
            if (w.OutputLineNo is null)
            {
                var lastGood = order.Operations.Count > 0 ? order.Operations.Last().GoodQty : (decimal?)null;
                var ceiling = ReceiptLimits.MainCeiling(order.Quantity, setting.OverProductionTolerancePct, lastGood);
                var remaining = ReceiptLimits.Remaining(ceiling, order.CompletedQty, PendingReceipt(null));
                MfgRules.ThrowIf(wanted.Where(x => x.OutputLineNo is null).Sum(x => x.Qty) > remaining, MfgErrors.Fg001, ("max", remaining));
            }
            else
            {
                var output = outputs[w.OutputLineNo.Value];
                var ceiling = ReceiptLimits.OutputCeiling(output.PlannedQty, setting.OverProductionTolerancePct);
                var remaining = ReceiptLimits.Remaining(ceiling, output.ReceivedQty, PendingReceipt(output.Id));
                MfgRules.ThrowIf(wanted.Where(x => x.OutputLineNo == w.OutputLineNo).Sum(x => x.Qty) > remaining, MfgErrors.Fg001, ("max", remaining));
            }
        }

        // lots, warehouse, quality hold and dates (BR-FG-02, BR-FG-04)
        var taken = new Dictionary<Guid, HashSet<string>>();
        var receiptLines = new List<NewStockLine>();
        foreach (var w in wanted.OrderBy(x => x.OutputLineNo is null ? 0 : 1).ThenBy(x => x.OutputLineNo))
        {
            var output = w.OutputLineNo is null ? null : outputs[w.OutputLineNo.Value];
            var itemId = output?.ItemId ?? order.ItemId;
            var item = known[itemId];
            var profile = await profiles.GetAsync(itemId, plant.Id, ct);
            var fqc = profile?.RequiresFqc == true;
            var needsLot = item.IsLotControlled || fqc;

            string? lotNumber = null;
            DateTime? expiry = null;
            if (needsLot)
            {
                if (!taken.TryGetValue(itemId, out var used))
                {
                    used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var lot in await stock.ListLotsAsync(itemId, ct)) used.Add(lot.LotNumber);
                    foreach (var number in await documents.LotNumbersInUseAsync(itemId, ct)) used.Add(number);
                    taken[itemId] = used;
                }
                if (w.Given is not null)
                {
                    MfgRules.ThrowIf(used.Contains(w.Given), MfgErrors.Fg002, ("lot", w.Given));
                    lotNumber = w.Given;
                }
                else lotNumber = LotNumberFormatter.Next(setting.FgLotNumberFormat, item.Sku, today, used);
                used.Add(lotNumber);
                if (profile is { ShelfLifeDays: > 0 }) expiry = manufactured.AddDays(profile.ShelfLifeDays);
            }

            var warehouse = fqc ? plant.QuarantineWarehouseId : order.OutputWarehouseId;
            receiptLines.Add(NewStockLine.Receipt(itemId, item.Sku, warehouse, w.Qty, output?.Id, output?.LineNo, lotNumber, expiry, needsLot ? manufactured : null, fqc));
        }

        // BR-FG-05 / BR-MAT-08: the backflush materials no operation owns are consumed by the quantity received; a shortage refuses the whole receipt
        var consumption = new List<NewStockLine>();
        var mainQty = wanted.Where(w => w.OutputLineNo is null).Sum(w => w.Qty);
        var targets = BackflushPlanner.ReceiptTargetsOf(order);
        if (mainQty > 0 && targets.Count > 0)
            consumption = await planner.PlanAsync(order, targets, order.CompletedQty + PendingReceipt(null) + mainQty, flows, setting.AllowNegativeStockOnBackflush, now, ct);

        var documentLines = consumption.Concat(receiptLines).ToList();   // consumption first: a refusal later reverses it with a plain return
        var docNo = await numbers.NextAsync(DocumentTypes.FinishedGoodsReceipt, Guid.Empty, today.ToDateTime(TimeOnly.MinValue), ct);   // per tenant: the number is the Warehouse key
        var document = MaterialTransaction.Create(docNo, StockDocumentType.Receipt, plant.Id, order.Id, order.OrderNo, today, null, r.Note, r.IdempotencyKey, hash, documentLines, by);
        document.Claim(now + MaterialDocuments.FirstAttemptDelay);
        document = await MaterialDocuments.StoreAsync(documents, uow, document, r.IdempotencyKey, hash, order.Id, ct);
        if (document.Status != StockDocumentStatus.Pending || document.Attempts > 0 || document.PostedLines.Any())
            return new StockDocumentResultDto(StockDocumentDto.From(document), document.Status == StockDocumentStatus.Pending);

        return MaterialDocuments.Conclude(document, await posting.PostAsync(document, by, ct));
    }

    private static string Hash(Guid orderId, IReadOnlyList<ReceiptLineBody> lines, string? note)
    {
        var text = $"Receipt|{orderId}|{note?.Trim()}|" + string.Join(';', lines.OrderBy(l => l.OutputLineNo).ThenBy(l => l.LotNumber).Select(l => $"{l.OutputLineNo}:{l.Quantity:0.######}:{l.LotNumber?.Trim()}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}

// ── Interim quality release (replaced by FQC in Phase 4) ─────────────────────────────────────────────────────

/// <summary>
/// Releases the lots of a posted receipt that are on quality hold: each moves to the order's finished-goods warehouse and becomes Available. Two idempotent Warehouse calls per
/// line, so a release that stopped halfway is finished by calling again. Needs <c>manufacturing:qc:disposition</c>.
/// </summary>
public sealed record QcReleaseReceiptCommand(Guid DocumentId) : IRequest<StockDocumentDto>;

public sealed class QcReleaseReceiptHandler(
    IPlantAccessService access, IStockDocumentRepository documents, IProductionOrderRepository orders, IInspectionRepository inspections, IQuarantineRelease release, IPlantClock clock, IMfgUnitOfWork uow)
    : IRequestHandler<QcReleaseReceiptCommand, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(QcReleaseReceiptCommand r, CancellationToken ct)
    {
        var document = await StockDocumentScope.LoadAsync(documents, access, r.DocumentId, ct);
        if (document.Type != StockDocumentType.Receipt || document.Status != StockDocumentStatus.Posted)
            throw MfgRules.Invalid("Only a posted finished-goods receipt can be released from quality hold.");
        var order = await orders.GetAsync(document.OrderId, ct) ?? throw new EntityNotFoundException(nameof(ProductionOrder), document.OrderId);

        var held = document.ReceiptLines.Where(l => l.Status == StockLineStatus.Posted && l.IsQcHold && l.QcReleasedAt is null).ToList();
        // P4-3: a line that has an inspection is released through it, never by hand (a cancelled inspection does not count)
        var inspected = (await inspections.ListForReceiptLinesAsync(held.Select(l => l.Id).ToList(), ct)).FirstOrDefault(i => i.Status != InspectionStatus.Cancelled);
        MfgRules.ThrowIf(inspected is not null, MfgErrors.Qc004, ("inspection", inspected?.DocNo));

        foreach (var line in held)
        {
            await release.ReleaseAsync(order, line, $"Quality release of {line.MovementKey}", ct);
            document.RecordQcReleased(line, clock.UtcNow);
            await uow.SaveChangesAsync(ct);                                              // progress is durable
        }
        return StockDocumentDto.From(document);
    }
}

// ── Complete, reopen, close (BR-PO-14..16) ───────────────────────────────────────────────────────────────

public sealed record CompleteOrderCommand(Guid Id, Guid? ReasonCodeId, string? Note) : IRequest<OrderActionResultDto>;

public sealed class CompleteOrderHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IStockDocumentRepository documents, IOperationReportRepository reports, IReasonCodeRepository reasons,
    IProductionSettingProvider settings, OperationLogCloser logCloser, IOrderReservationSync sync, IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<CompleteOrderCommand, OrderActionResultDto>
{
    public async Task<OrderActionResultDto> Handle(CompleteOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        var setting = await settings.GetAsync(order.PlantId, ct);
        var reason = r.ReasonCodeId is { } id && id != Guid.Empty ? await reasons.GetAsync(id, ct) : null;
        var isShortClose = reason is { IsActive: true, Type: ReasonType.ShortClose };
        var pending = await documents.CountPendingAsync(order.Id, ct) + (await reports.ListPendingForOrderAsync(order.Id, ct)).Count;

        order.Complete(user.GetUserId(), clock.UtcNow, setting.UnderCompletionTolerancePct, user.HasPermission(PermissionConstants.Manufacturing_OrderShortClose), isShortClose,
            r.ReasonCodeId, r.Note, pending);
        await logCloser.CloseForCompletedAsync(order, ct);
        return await OrderTransitions.SaveAndSyncAsync(order, uow, sync, null, ct);
    }
}

public sealed record ReopenOrderCommand(Guid Id) : IRequest<OrderActionResultDto>;

public sealed class ReopenOrderHandler(IPlantAccessService access, IProductionOrderRepository orders, IOrderReservationSync sync, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<ReopenOrderCommand, OrderActionResultDto>
{
    public async Task<OrderActionResultDto> Handle(ReopenOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        order.Reopen(user.GetUserId());
        return await OrderTransitions.SaveAndSyncAsync(order, uow, sync, null, ct);
    }
}

public sealed record CloseConditionDto(string Code, bool Met, string Message);

public sealed record CloseChecklistDto(Guid OrderId, string OrderNo, OrderStatus Status, bool CanClose, IReadOnlyList<CloseConditionDto> Conditions);

internal static class CloseChecks
{
    public static async Task<IReadOnlyList<CloseCondition>> ConditionsAsync(ProductionOrder order, ITimeLogRepository logs, IStockDocumentRepository documents, IOperationReportRepository reports,
        bool confirmConsumeRemaining, CancellationToken ct)
    {
        var openLogs = (await logs.ListOpenForOrderAsync(order.Id, ct)).Count(l => l.IsOpen);
        var held = await documents.CountHeldLotsAsync(order.Id, ct);
        var pending = await documents.CountPendingAsync(order.Id, ct) + (await reports.ListPendingForOrderAsync(order.Id, ct)).Count;
        return order.CloseConditions(openLogs, held, pending, confirmConsumeRemaining);
    }
}

public sealed record GetCloseChecklistQuery(Guid Id, bool ConfirmConsumeRemaining = false) : IRequest<CloseChecklistDto>;

public sealed class GetCloseChecklistHandler(IPlantAccessService access, IProductionOrderRepository orders, ITimeLogRepository logs, IStockDocumentRepository documents, IOperationReportRepository reports)
    : IRequestHandler<GetCloseChecklistQuery, CloseChecklistDto>
{
    public async Task<CloseChecklistDto> Handle(GetCloseChecklistQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        var conditions = await CloseChecks.ConditionsAsync(order, logs, documents, reports, r.ConfirmConsumeRemaining, ct);
        return new CloseChecklistDto(order.Id, order.OrderNo, order.Status, order.Status == OrderStatus.Completed && conditions.All(c => c.Met), conditions.Select(c => new CloseConditionDto(c.Code, c.Met, c.Message)).ToList());
    }
}

public sealed record CloseOrderCommand(Guid Id, bool ConfirmConsumeRemaining) : IRequest<OrderDto>;

public sealed class CloseOrderHandler(IPlantAccessService access, IProductionOrderRepository orders, ITimeLogRepository logs, IStockDocumentRepository documents, IOperationReportRepository reports,
    IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<CloseOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CloseOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        order.EnsureEditable();
        var openLogs = (await logs.ListOpenForOrderAsync(order.Id, ct)).Count(l => l.IsOpen);
        var held = await documents.CountHeldLotsAsync(order.Id, ct);
        var pending = await documents.CountPendingAsync(order.Id, ct) + (await reports.ListPendingForOrderAsync(order.Id, ct)).Count;
        order.Close(user.GetUserId(), openLogs, held, pending, r.ConfirmConsumeRemaining);
        await uow.SaveChangesAsync(ct);
        return OrderDto.From(order);
    }
}
