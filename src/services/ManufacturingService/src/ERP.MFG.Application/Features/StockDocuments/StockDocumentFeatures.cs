using System.Security.Cryptography;
using System.Text;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.StockDocuments;

// ── DTOs ─────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record StockDocumentLineDto(
    int LineNo, Guid? OrderMaterialId, int? MaterialLineNo, Guid ItemId, string ItemCode, Guid WarehouseId, Guid? LotId, decimal Qty, bool IsFromReservation, string MovementKey,
    StockLineStatus Status, decimal? UnitCost, decimal? Amount, Guid? PostedLotId, string? Error, StockLineKind Kind = StockLineKind.Material, int? OutputLineNo = null, string? LotNumber = null,
    DateTime? ExpiryDate = null, bool IsQcHold = false, DateTime? QcReleasedAt = null, bool IsNegativeStock = false, DateTime? ManufactureDate = null)
{
    public static StockDocumentLineDto From(MaterialTransactionLine l) => new(l.LineNo, l.OrderMaterialId, l.MaterialLineNo, l.ItemId, l.ItemCode, l.WarehouseId, l.LotId, l.Qty,
        l.IsFromReservation, l.MovementKey, l.Status, l.UnitCost, l.Amount, l.PostedLotId, l.Error, l.Kind, l.OutputLineNo, l.LotNumber, l.ExpiryDate, l.IsQcHold, l.QcReleasedAt, l.IsNegativeStock,
        l.ManufactureDate);
}

public sealed record StockDocumentDto(
    Guid Id, string DocNo, Guid PlantId, Guid OrderId, string OrderNo, StockDocumentType Type, StockDocumentStatus Status, StockDocumentMode Mode, DateOnly PostingDate, Guid? ReasonCodeId,
    string? Note, int Attempts, DateTime? NextAttemptAt, string? LastError, string? LastErrorCode, DateTime? PostedAt, DateTime CreatedAt, string? CreatedBy, IReadOnlyList<StockDocumentLineDto> Lines)
{
    public static StockDocumentDto From(MaterialTransaction d) => new(d.Id, d.DocNo, d.PlantId, d.OrderId, d.OrderNo, d.Type, d.Status, d.Mode, d.PostingDate, d.ReasonCodeId, d.Note,
        d.Attempts, d.NextAttemptAt, d.LastError, d.LastErrorCode, d.PostedAt, d.CreatedAt, d.CreatedBy, d.Lines.Select(StockDocumentLineDto.From).ToList());
}

/// <summary>The answer of a posting call: 201 when the document is Posted, 202 when it is still Pending (the Warehouse will be told again).</summary>
public sealed record StockDocumentResultDto(StockDocumentDto Document, bool IsPending);

public sealed record MaterialLineBody(int LineNo, decimal Quantity, Guid? LotId);

public sealed record ReservedLotDto(Guid LotId, string? LotNumber, decimal Remaining);
public sealed record MaterialLotSuggestionDto(int LineNo, string ComponentCode, Guid WarehouseId, decimal Quantity, decimal ReservedRemaining, IReadOnlyList<ReservedLotDto> ReservedLots, LotSuggestion Suggestion);

// ── Shared checks ────────────────────────────────────────────────────────────────────────────────────────────

internal static class MaterialDocuments
{
    public const int MaxKeyLength = 100;
    public static readonly TimeSpan FirstAttemptDelay = TimeSpan.FromSeconds(20);

    /// <summary>
    /// BR-MAT-01: only Released and InProgress orders take material; OnHold has its own code, Closed and Cancelled are final. A <b>return</b> is also accepted on a Completed order
    /// (deviation, P3-5): unused material is given back before the order is closed.
    /// </summary>
    public static void EnsureOpenForMaterial(ProductionOrder order, bool isReturn = false)
    {
        order.EnsureEditable();
        MfgRules.ThrowIf(order.Status == OrderStatus.OnHold, MfgErrors.Po011);
        var allowed = order.Status is OrderStatus.Released or OrderStatus.InProgress || (isReturn && order.Status == OrderStatus.Completed);
        MfgRules.ThrowIf(!allowed, MfgErrors.Mat001);
    }

    public static string Hash(StockDocumentType type, Guid orderId, IReadOnlyList<MaterialLineBody> lines, Guid? reasonCodeId, string? note)
    {
        var text = $"{type}|{orderId}|{reasonCodeId}|{note?.Trim()}|" + string.Join(';', lines.OrderBy(l => l.LineNo).ThenBy(l => l.LotId).Select(l => $"{l.LineNo}:{l.Quantity:0.######}:{l.LotId}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>The same Idempotency-Key with the same request answers the first document; with another request it is a conflict (<c>MFG_GEN_007</c>).</summary>
    public static async Task<MaterialTransaction?> FindRepeatAsync(IStockDocumentRepository documents, string? key, string hash, Guid orderId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(key)) return null;
        MfgRules.ThrowIf(key.Length > MaxKeyLength, MfgErrors.Gen007);
        var existing = await documents.FindByKeyAsync(key, ct);
        if (existing is null) return null;
        MfgRules.ThrowIf(existing.RequestHash != hash || existing.OrderId != orderId, MfgErrors.Gen007);
        return existing;
    }

    /// <summary>Merges lines for the same material and lot, checks each belongs to the order and has a positive quantity within the unit's decimals.</summary>
    public static async Task<List<(ProductionOrderMaterial Material, Guid? LotId, decimal Qty)>> ResolveLinesAsync(
        ProductionOrder order, IReadOnlyList<MaterialLineBody> lines, IUnitOfMeasureClient units, CancellationToken ct)
    {
        MfgRules.ThrowIf(lines is null || lines.Count == 0, MfgErrors.Mat001);
        var resolved = new List<(ProductionOrderMaterial, Guid?, decimal)>();
        var decimals = (await units.LookupAsync(order.Materials.Select(m => m.UomId).Distinct().ToList(), ct)).ToDictionary(u => u.Id, u => u.DecimalPlaces);
        foreach (var group in lines.GroupBy(l => (l.LineNo, LotId: l.LotId == Guid.Empty ? null : l.LotId)))
        {
            var material = order.Materials.FirstOrDefault(m => m.LineNo == group.Key.LineNo)
                ?? throw MfgRules.Invalid($"Order {order.OrderNo} has no material line {group.Key.LineNo}.");
            var qty = group.Sum(l => l.Quantity);
            MfgRules.ThrowIf(qty <= 0, MfgErrors.Mat001);
            var places = decimals.GetValueOrDefault(material.UomId, 6);
            if (Math.Round(qty, places) != qty)
                throw MfgRules.Invalid($"The quantity of {material.ComponentCode} can have at most {places} decimal places.");
            resolved.Add((material, group.Key.LotId, qty));
        }
        return resolved;
    }

    public static async Task<Dictionary<Guid, ProductInfo>> ProductsAsync(IProductLookupClient products, IEnumerable<ProductionOrderMaterial> materials, CancellationToken ct)
    {
        var ids = materials.Select(m => m.ComponentItemId).Distinct().ToList();
        var known = (await products.LookupAsync(ids, ct)).ToDictionary(p => p.Id);
        foreach (var m in materials)
        {
            if (!known.TryGetValue(m.ComponentItemId, out var p)) throw MfgRules.Invalid($"Item {m.ComponentCode} does not exist any more.");
            MfgRules.ThrowIf(p.IsSerialControlled, MfgErrors.Mat011, ("item", p.Sku));
        }
        return known;
    }

    /// <summary>Stores a new document (or, when a parallel request with the same Idempotency-Key won the race, returns that one).</summary>
    public static async Task<MaterialTransaction> StoreAsync(IStockDocumentRepository documents, IMfgUnitOfWork uow, MaterialTransaction document, string? key, string hash, Guid orderId, CancellationToken ct)
    {
        documents.Add(document);
        try
        {
            await uow.SaveChangesAsync(ct);
            return document;
        }
        catch (ConcurrencyException) when (!string.IsNullOrEmpty(key))
        {
            var winner = await FindRepeatAsync(documents, key, hash, orderId, ct);
            return winner ?? throw new ConcurrencyException(MfgErrors.Format(MfgErrors.Gen001));
        }
    }

    /// <summary>The end of a posting call: Failed becomes the refusal the user sees (the document stays in the list), Posted and Pending are returned.</summary>
    public static StockDocumentResultDto Conclude(MaterialTransaction document, StockPostingResult result)
    {
        if (result.Status == StockDocumentStatus.Failed)
            throw new BusinessRuleException(result.ErrorCode ?? MfgErrors.Mat003, result.Error ?? "The Warehouse refused the document.");
        return new StockDocumentResultDto(StockDocumentDto.From(document), result.Status == StockDocumentStatus.Pending);
    }
}

// ── Material issue (BR-MAT-01..04, 06, 07) ───────────────────────────────────────────────────────────────

public sealed record IssueMaterialCommand(Guid OrderId, IReadOnlyList<MaterialLineBody> Lines, Guid? ReasonCodeId, string? Note, string? IdempotencyKey) : IRequest<StockDocumentResultDto>;

public sealed class IssueMaterialHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IStockDocumentRepository documents, IWarehouseStockClient stock, IProductLookupClient products,
    IUnitOfMeasureClient units, IProductionSettingProvider settings, IReasonCodeRepository reasons, IOrderReservationSync sync, IDocumentNumberService numbers, IPlantClock clock,
    IStockPostingService posting, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<IssueMaterialCommand, StockDocumentResultDto>
{
    public async Task<StockDocumentResultDto> Handle(IssueMaterialCommand r, CancellationToken ct)
    {
        var hash = MaterialDocuments.Hash(StockDocumentType.Issue, r.OrderId, r.Lines ?? [], r.ReasonCodeId, r.Note);
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var repeat = await MaterialDocuments.FindRepeatAsync(documents, r.IdempotencyKey, hash, order.Id, ct);
        if (repeat is not null) return new StockDocumentResultDto(StockDocumentDto.From(repeat), repeat.Status == StockDocumentStatus.Pending);

        MaterialDocuments.EnsureOpenForMaterial(order);
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var wanted = await MaterialDocuments.ResolveLinesAsync(order, r.Lines ?? [], units, ct);
        var items = await MaterialDocuments.ProductsAsync(products, wanted.Select(w => w.Material), ct);

        // BR-MAT-07: an issue consumes the reservation, so the reservation must be the one the order believes in (R218)
        if (order.ReservationSyncPending)
        {
            await sync.SyncAsync(order, ct);
            MfgRules.ThrowIf(order.ReservationSyncPending, MfgErrors.Mat012);
        }

        var setting = await settings.GetAsync(plant.Id, ct);
        var flows = await documents.FlowsAsync(order.Id, ct);
        var overIssueReason = await OverIssueReasonAsync(r.ReasonCodeId, ct);
        var reservation = await stock.GetReservationAsync(order.Id, ct);
        var open = reservation is { Status: "Reserved" } ? reservation : null;
        var now = clock.UtcNow;

        // BR-MAT-02: net issued (plus issues still on their way) may not pass Required x (1 + tolerance) unless the caller may over-issue and says why
        foreach (var byMaterial in wanted.GroupBy(w => w.Material))
        {
            var material = byMaterial.Key;
            var pendingIssue = PendingQty(flows, material.Id, StockDocumentType.Issue);
            if (MaterialLimits.ExceedsTolerance(material.RequiredQty, setting.OverIssueTolerancePct, material.NetIssuedQty, pendingIssue, byMaterial.Sum(w => w.Qty)))
                MfgRules.ThrowIf(!user.HasPermission(PermissionConstants.Manufacturing_MaterialOverIssue) || !overIssueReason,
                    MfgErrors.Mat002, ("max", MaterialLimits.MaxNetIssue(material.RequiredQty, setting.OverIssueTolerancePct)));
        }

        // BR-MAT-03/04: stock, lots and the reservation decide how each line is taken
        var lines = new List<NewStockLine>();
        foreach (var (material, lotId, qty) in wanted)
        {
            var item = items[material.ComponentItemId];
            var availability = await stock.GetAvailabilityAsync(material.ComponentItemId, material.WarehouseId, ct);
            var held = open?.Lines.Where(l => l.ProductId == material.ComponentItemId && l.SourceLineId == material.Id).ToList() ?? [];
            var split = Plan(material, item, lotId, qty, availability, held, now);
            if (split.Reserved > 0) lines.Add(new NewStockLine(material.Id, material.LineNo, material.ComponentItemId, material.ComponentCode, material.WarehouseId, lotId, split.Reserved, true, null));
            if (split.Free > 0) lines.Add(new NewStockLine(material.Id, material.LineNo, material.ComponentItemId, material.ComponentCode, material.WarehouseId, lotId, split.Free, false, null));
        }

        var today = clock.Today(plant);
        var docNo = await numbers.NextAsync(DocumentTypes.MaterialIssue, Guid.Empty, today.ToDateTime(TimeOnly.MinValue), ct);   // per tenant: the number is the Warehouse key
        var document = MaterialTransaction.Create(docNo, StockDocumentType.Issue, plant.Id, order.Id, order.OrderNo, today, r.ReasonCodeId, r.Note, r.IdempotencyKey, hash, lines, user.GetUserId());
        document.Claim(now + MaterialDocuments.FirstAttemptDelay);                       // if this request dies, the job takes the document up after a short while
        document = await MaterialDocuments.StoreAsync(documents, uow, document, r.IdempotencyKey, hash, order.Id, ct);
        if (document.Status != StockDocumentStatus.Pending || document.Attempts > 0 || document.PostedLines.Any())
            return new StockDocumentResultDto(StockDocumentDto.From(document), document.Status == StockDocumentStatus.Pending);

        return MaterialDocuments.Conclude(document, await posting.PostAsync(document, user.GetUserId(), ct));
    }

    private async Task<bool> OverIssueReasonAsync(Guid? id, CancellationToken ct)
    {
        if (id is null || id == Guid.Empty) return false;
        var reason = await reasons.GetAsync(id.Value, ct);
        return reason is { IsActive: true, Type: ReasonType.OverIssue };
    }

    private static decimal PendingQty(IReadOnlyList<StockFlowLine> flows, Guid materialId, StockDocumentType type)
        => flows.Where(f => f.OrderMaterialId == materialId && f.Type == type && f.DocumentStatus == StockDocumentStatus.Pending && f.DocumentMode == StockDocumentMode.Forward).Sum(f => f.Qty);

    /// <summary>How much of the line leaves the order's reservation and how much from free stock; refuses what the stock cannot give.</summary>
    internal static (decimal Reserved, decimal Free) Plan(ProductionOrderMaterial material, ProductInfo item, Guid? lotId, decimal qty, StockAvailability availability,
        IReadOnlyList<StockReservationLineResult> held, DateTime now)
    {
        var rows = availability.Balances.Where(b => b.WarehouseId == material.WarehouseId).ToList();
        decimal ownRemaining, freeCapacity;

        if (lotId is { } lot)
        {
            var row = rows.FirstOrDefault(b => b.LotId == lot) ?? throw MfgRules.Fail(MfgErrors.Mat005, ("lot", lot));
            if (row.LotStatus != "Available" || (row.ExpiryDate is { } expiry && expiry <= now)) throw MfgRules.Fail(MfgErrors.Mat005, ("lot", row.LotNumber ?? lot.ToString()));
            ownRemaining = held.SelectMany(h => h.Lots).Where(l => l.LotId == lot).Sum(l => l.Remaining);
            freeCapacity = Math.Max(row.AvailableQuantity, 0m);
        }
        else
        {
            ownRemaining = item.IsLotControlled ? held.SelectMany(h => h.Lots).Sum(l => l.Remaining) : held.Sum(h => h.Remaining);
            var usable = rows.Where(b => b.LotId == null || (b.LotStatus == "Available" && (b.ExpiryDate is null || b.ExpiryDate > now))).Sum(b => Math.Max(b.AvailableQuantity, 0m));
            freeCapacity = Math.Min(usable, Math.Max(availability.AvailableQuantity, 0m));
        }

        var reserved = Math.Min(qty, ownRemaining);
        var free = qty - reserved;
        MfgRules.ThrowIf(free > 0 && item.IsLotControlled && lotId is null, MfgErrors.Mat004, ("item", item.Sku));
        MfgRules.ThrowIf(free > freeCapacity, MfgErrors.Mat003, ("available", reserved + freeCapacity));
        return (reserved, free);
    }
}

// ── Material return (BR-MAT-05, 07) ──────────────────────────────────────────────────────────────────────

public sealed record ReturnMaterialCommand(Guid OrderId, IReadOnlyList<MaterialLineBody> Lines, Guid? ReasonCodeId, string? Note, string? IdempotencyKey) : IRequest<StockDocumentResultDto>;

public sealed class ReturnMaterialHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IStockDocumentRepository documents, IProductLookupClient products, IUnitOfMeasureClient units,
    IDocumentNumberService numbers, IPlantClock clock, IStockPostingService posting, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<ReturnMaterialCommand, StockDocumentResultDto>
{
    public async Task<StockDocumentResultDto> Handle(ReturnMaterialCommand r, CancellationToken ct)
    {
        var hash = MaterialDocuments.Hash(StockDocumentType.Return, r.OrderId, r.Lines ?? [], r.ReasonCodeId, r.Note);
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var repeat = await MaterialDocuments.FindRepeatAsync(documents, r.IdempotencyKey, hash, order.Id, ct);
        if (repeat is not null) return new StockDocumentResultDto(StockDocumentDto.From(repeat), repeat.Status == StockDocumentStatus.Pending);

        MaterialDocuments.EnsureOpenForMaterial(order, isReturn: true);
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var wanted = await MaterialDocuments.ResolveLinesAsync(order, r.Lines ?? [], units, ct);
        var items = await MaterialDocuments.ProductsAsync(products, wanted.Select(w => w.Material), ct);
        var flows = await documents.FlowsAsync(order.Id, ct);

        var lines = new List<NewStockLine>();
        foreach (var byMaterial in wanted.GroupBy(w => w.Material))
        {
            var material = byMaterial.Key;
            var item = items[material.ComponentItemId];
            var pendingReturn = flows.Where(f => f.OrderMaterialId == material.Id && f.Type == StockDocumentType.Return && f.DocumentStatus == StockDocumentStatus.Pending && f.DocumentMode == StockDocumentMode.Forward).Sum(f => f.Qty);
            var maxReturn = MaterialLimits.MaxReturn(material.IssuedQty, material.ReturnedQty, pendingReturn);
            MfgRules.ThrowIf(byMaterial.Sum(w => w.Qty) > maxReturn, MfgErrors.Mat006, ("issued", maxReturn));

            foreach (var (_, lotId, qty) in byMaterial)
            {
                var lot = item.IsLotControlled ? lotId : null;
                MfgRules.ThrowIf(item.IsLotControlled && lot is null, MfgErrors.Mat004, ("item", item.Sku));
                if (lot is { } l) MfgRules.ThrowIf(qty > LotCap(flows, material.Id, l, pendingReturn: LotPendingReturn(flows, material.Id, l)), MfgErrors.Mat006, ("issued", LotCap(flows, material.Id, l, LotPendingReturn(flows, material.Id, l))));
                var cost = MaterialLimits.AverageIssueCost(flows
                    .Where(f => f.OrderMaterialId == material.Id && f.Type == StockDocumentType.Issue && f.DocumentStatus == StockDocumentStatus.Posted && f.LineStatus == StockLineStatus.Posted && (lot is null || f.LotId == lot))
                    .Select(f => (f.Qty, f.UnitCost)));
                lines.Add(new NewStockLine(material.Id, material.LineNo, material.ComponentItemId, material.ComponentCode, material.WarehouseId, lot, qty, false, cost));
            }
        }

        var now = clock.UtcNow;
        var today = clock.Today(plant);
        var docNo = await numbers.NextAsync(DocumentTypes.MaterialReturn, Guid.Empty, today.ToDateTime(TimeOnly.MinValue), ct);
        var document = MaterialTransaction.Create(docNo, StockDocumentType.Return, plant.Id, order.Id, order.OrderNo, today, r.ReasonCodeId, r.Note, r.IdempotencyKey, hash, lines, user.GetUserId());
        document.Claim(now + MaterialDocuments.FirstAttemptDelay);
        document = await MaterialDocuments.StoreAsync(documents, uow, document, r.IdempotencyKey, hash, order.Id, ct);
        if (document.Status != StockDocumentStatus.Pending || document.Attempts > 0 || document.PostedLines.Any())
            return new StockDocumentResultDto(StockDocumentDto.From(document), document.Status == StockDocumentStatus.Pending);

        return MaterialDocuments.Conclude(document, await posting.PostAsync(document, user.GetUserId(), ct));
    }

    private static decimal LotPendingReturn(IReadOnlyList<StockFlowLine> flows, Guid materialId, Guid lot)
        => flows.Where(f => f.OrderMaterialId == materialId && f.Type == StockDocumentType.Return && f.DocumentStatus == StockDocumentStatus.Pending && f.DocumentMode == StockDocumentMode.Forward && f.LotId == lot).Sum(f => f.Qty);

    /// <summary>
    /// What may still go back to a lot: what was issued from it minus what already went back, minus returns waiting; issues whose lot is unknown (drawn from several lots
    /// by the reservation) may go to any lot, so they widen every lot's cap.
    /// </summary>
    internal static decimal LotCap(IReadOnlyList<StockFlowLine> flows, Guid materialId, Guid lot, decimal pendingReturn)
    {
        var posted = flows.Where(f => f.OrderMaterialId == materialId && f.DocumentStatus == StockDocumentStatus.Posted && f.LineStatus == StockLineStatus.Posted).ToList();
        var issuedFromLot = posted.Where(f => f.Type == StockDocumentType.Issue && f.LotId == lot).Sum(f => f.Qty);
        var unattributed = posted.Where(f => f.Type == StockDocumentType.Issue && f.LotId == null).Sum(f => f.Qty);
        var returnedToLot = posted.Where(f => f.Type == StockDocumentType.Return && f.LotId == lot).Sum(f => f.Qty);
        return Math.Max(issuedFromLot + unattributed - returnedToLot - pendingReturn, 0m);
    }
}

// ── Retry / abandon (permission stock-document:retry) ────────────────────────────────────────────────────────

public sealed record RetryStockDocumentCommand(Guid Id) : IRequest<StockDocumentResultDto>;

public sealed class RetryStockDocumentHandler(IPlantAccessService access, IStockDocumentRepository documents, IStockPostingService posting, IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<RetryStockDocumentCommand, StockDocumentResultDto>
{
    public async Task<StockDocumentResultDto> Handle(RetryStockDocumentCommand r, CancellationToken ct)
    {
        var document = await StockDocumentScope.LoadAsync(documents, access, r.Id, ct);
        document.RetryNow(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return MaterialDocuments.Conclude(document, await posting.PostAsync(document, user.GetUserId(), ct));
    }
}

public sealed record AbandonStockDocumentCommand(Guid Id) : IRequest<StockDocumentResultDto>;

public sealed class AbandonStockDocumentHandler(IPlantAccessService access, IStockDocumentRepository documents, IStockPostingService posting, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<AbandonStockDocumentCommand, StockDocumentResultDto>
{
    public async Task<StockDocumentResultDto> Handle(AbandonStockDocumentCommand r, CancellationToken ct)
    {
        var document = await StockDocumentScope.LoadAsync(documents, access, r.Id, ct);
        document.Abandon(user.GetUserId());
        await uow.SaveChangesAsync(ct);
        var result = await posting.PostAsync(document, user.GetUserId(), ct);
        // an abandoned document ends Failed on purpose: that is the answer, not an error
        return new StockDocumentResultDto(StockDocumentDto.From(document), result.Status == StockDocumentStatus.Pending);
    }
}

internal static class StockDocumentScope
{
    /// <summary>A document of a plant the caller may see; otherwise it does not exist for them (404).</summary>
    public static async Task<MaterialTransaction> LoadAsync(IStockDocumentRepository documents, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var document = await documents.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(MaterialTransaction), id);
        await access.GetVisiblePlantAsync(document.PlantId, ct);
        return document;
    }
}

// ── Reads ─────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetStockDocumentQuery(Guid Id) : IRequest<StockDocumentDto>;

public sealed class GetStockDocumentHandler(IPlantAccessService access, IStockDocumentRepository documents) : IRequestHandler<GetStockDocumentQuery, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(GetStockDocumentQuery r, CancellationToken ct) => StockDocumentDto.From(await StockDocumentScope.LoadAsync(documents, access, r.Id, ct));
}

public sealed record GetStockDocumentsQuery(Guid PlantId, StockDocumentStatus? Status, StockDocumentType? Type, Guid? OrderId, string? Search, int Page, int PageSize) : IRequest<PagedStockDocuments>;

public sealed record PagedStockDocuments(IReadOnlyList<StockDocumentDto> Items, int Total, int Page, int PageSize);

public sealed class GetStockDocumentsHandler(IPlantAccessService access, IStockDocumentRepository documents) : IRequestHandler<GetStockDocumentsQuery, PagedStockDocuments>
{
    public async Task<PagedStockDocuments> Handle(GetStockDocumentsQuery r, CancellationToken ct)
    {
        await access.GetVisiblePlantAsync(r.PlantId, ct);
        var page = Math.Max(r.Page, 1);
        var size = Math.Clamp(r.PageSize, 1, 200);
        var (items, total) = await documents.ListAsync(new StockDocumentFilter(r.PlantId, r.Status, r.Type, r.OrderId, r.Search), page, size, ct);
        return new PagedStockDocuments(items.Select(StockDocumentDto.From).ToList(), total, page, size);
    }
}

public sealed record GetOrderStockDocumentsQuery(Guid OrderId) : IRequest<IReadOnlyList<StockDocumentDto>>;

public sealed class GetOrderStockDocumentsHandler(IPlantAccessService access, IProductionOrderRepository orders, IStockDocumentRepository documents)
    : IRequestHandler<GetOrderStockDocumentsQuery, IReadOnlyList<StockDocumentDto>>
{
    public async Task<IReadOnlyList<StockDocumentDto>> Handle(GetOrderStockDocumentsQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        return (await documents.ListForOrderAsync(order.Id, ct)).Select(StockDocumentDto.From).ToList();
    }
}

public sealed record SuggestMaterialLotsQuery(Guid OrderId, int LineNo, decimal Quantity) : IRequest<MaterialLotSuggestionDto>;

/// <summary>First-expiry-first lots for a material line (BR-MAT-04), plus the lots the order's own reservation holds for it.</summary>
public sealed class SuggestMaterialLotsHandler(IPlantAccessService access, IProductionOrderRepository orders, IWarehouseStockClient stock)
    : IRequestHandler<SuggestMaterialLotsQuery, MaterialLotSuggestionDto>
{
    public async Task<MaterialLotSuggestionDto> Handle(SuggestMaterialLotsQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var material = order.Materials.FirstOrDefault(m => m.LineNo == r.LineNo) ?? throw MfgRules.Invalid($"Order {order.OrderNo} has no material line {r.LineNo}.");
        MfgRules.ThrowIf(r.Quantity <= 0, MfgErrors.Mat001);
        var suggestion = await stock.SuggestLotsAsync(material.ComponentItemId, r.Quantity, material.WarehouseId, ct);
        var reservation = await stock.GetReservationAsync(order.Id, ct);
        var held = reservation is { Status: "Reserved" }
            ? reservation.Lines.Where(l => l.ProductId == material.ComponentItemId && l.SourceLineId == material.Id).ToList()
            : [];
        var lots = held.SelectMany(h => h.Lots).Select(l => new ReservedLotDto(l.LotId, l.LotNumber, l.Remaining)).ToList();
        return new MaterialLotSuggestionDto(material.LineNo, material.ComponentCode, material.WarehouseId, r.Quantity, held.Sum(h => h.Remaining), lots, suggestion);
    }
}
