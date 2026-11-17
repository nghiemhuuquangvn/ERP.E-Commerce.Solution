using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Ncr;

// ── Non-conformance reports and their dispositions (BR-NCR-01..04, BR-RW-01..03), P4-4 ────────────────────────

public sealed record NcrDispositionDto(int LineNo, DispositionType Type, decimal Qty, Guid? ReasonCodeId, string? Note, string? ApprovedBy, DateTime? ApprovedAt, Guid? ReworkOrderId,
    string? ReworkOrderNo, OrderStatus? ReworkOrderStatus, string? MovementKey, Guid? ResultLotId, string? ResultLotNumber, bool IsExecuted, bool IsStarted, DateTime? ExecutedAt, string? ExecutedBy)
{
    public static NcrDispositionDto From(NcrDisposition d, ProductionOrder? rework = null) => new(d.LineNo, d.Type, d.Qty, d.ReasonCodeId, d.Note, d.ApprovedBy, d.ApprovedAt, d.ReworkOrderId,
        rework?.OrderNo, rework?.Status, d.MovementKey, d.ResultLotId, d.ResultLotNumber, d.IsExecuted, d.IsStarted, d.ExecutedAt, d.ExecutedBy);
}

public sealed record NcrListItemDto(Guid Id, string NcrNo, Guid PlantId, NcrSourceType SourceType, Guid? InspectionId, Guid? OperationReportId, Guid OrderId, string OrderNo, int? OperationSeq,
    Guid ItemId, string ItemCode, string ItemName, Guid? LotId, string? LotNumber, decimal NonconformingQty, string? Description, NcrStatus Status, bool IsInProcess, bool IsRecordOnly,
    DateTime CreatedAt, string? CreatedBy, DateTime? ClosedAt)
{
    public static NcrListItemDto From(NonConformance n) => new(n.Id, n.NcrNo, n.PlantId, n.SourceType, n.InspectionId, n.OperationReportId, n.OrderId, n.OrderNo, n.OperationSeq, n.ItemId, n.ItemCode,
        n.ItemName, n.LotId, n.LotNumber, n.NonconformingQty, n.Description, n.Status, n.IsInProcess, n.IsRecordOnly, n.CreatedAt, n.CreatedBy, n.ClosedAt);
}

public sealed record NcrDto(NcrListItemDto Ncr, IReadOnlyList<NcrDispositionDto> Dispositions)
{
    public static async Task<NcrDto> BuildAsync(NonConformance n, IProductionOrderRepository orders, CancellationToken ct)
    {
        var lines = new List<NcrDispositionDto>();
        foreach (var d in n.Dispositions)
            lines.Add(NcrDispositionDto.From(d, d.ReworkOrderId is { } id ? await orders.GetAsync(id, ct) : null));
        return new NcrDto(NcrListItemDto.From(n), lines);
    }
}

public sealed record PagedNcrs(List<NcrListItemDto> Items, int Total, int Page, int PageSize);

public sealed record DispositionBody(DispositionType Type, decimal Qty, Guid? ReasonCodeId, string? Note);

internal static class NcrScope
{
    public static async Task<NonConformance> LoadAsync(INcrRepository repo, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var ncr = await repo.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(NonConformance), id);
        await access.GetVisiblePlantAsync(ncr.PlantId, ct);
        return ncr;
    }
}

// ── Reads ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetNcrsQuery(Guid PlantId, NcrStatus? Status, NcrSourceType? Source, Guid? OrderId, string? Search, bool OpenOnly = false, int Page = 1, int PageSize = 50) : IRequest<PagedNcrs>;

public sealed class GetNcrsHandler(IPlantAccessService access, INcrRepository ncrs) : IRequestHandler<GetNcrsQuery, PagedNcrs>
{
    public async Task<PagedNcrs> Handle(GetNcrsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 200);
        var (items, total) = await ncrs.ListAsync(new NcrFilter(plant.Id, r.Status, r.Source, r.OrderId, r.Search, r.OpenOnly), page, size, ct);
        return new PagedNcrs(items.Select(NcrListItemDto.From).ToList(), total, page, size);
    }
}

public sealed record GetNcrQuery(Guid Id) : IRequest<NcrDto>;

public sealed class GetNcrHandler(IPlantAccessService access, INcrRepository ncrs, IProductionOrderRepository orders) : IRequestHandler<GetNcrQuery, NcrDto>
{
    public async Task<NcrDto> Handle(GetNcrQuery r, CancellationToken ct)
        => await NcrDto.BuildAsync(await NcrScope.LoadAsync(ncrs, access, r.Id, ct), orders, ct);
}

public sealed record GetOrderNcrsQuery(Guid OrderId) : IRequest<List<NcrListItemDto>>;

public sealed class GetOrderNcrsHandler(IPlantAccessService access, IProductionOrderRepository orders, INcrRepository ncrs) : IRequestHandler<GetOrderNcrsQuery, List<NcrListItemDto>>
{
    public async Task<List<NcrListItemDto>> Handle(GetOrderNcrsQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        return (await ncrs.ListForOrderAsync(order.Id, ct)).Select(NcrListItemDto.From).ToList();
    }
}

// ── Raise an NCR by hand from an operation report (T4.10.1, record only) ─────────────────────────────────────

public sealed record CreateManualNcrCommand(Guid OrderId, Guid ReportId, decimal Quantity, string Description) : IRequest<NcrDto>;

public sealed class CreateManualNcrHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IOperationReportRepository reports, INcrRepository ncrs, IDocumentNumberService numbers, IPlantClock clock,
    IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<CreateManualNcrCommand, NcrDto>
{
    public async Task<NcrDto> Handle(CreateManualNcrCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var report = await reports.GetAsync(r.ReportId, ct);
        if (report is null || report.OrderId != order.Id) throw new EntityNotFoundException(nameof(OperationReport), r.ReportId);
        MfgRules.ThrowIf(report.Status != OperationReportStatus.Posted || report.IsReversal || report.IsReversed, MfgErrors.Rev001, ("document", report.DocNo), ("reason", "chỉ lập phiếu cho báo cáo đã ghi nhận và chưa bị đảo"));

        var number = await numbers.NextAsync(DocumentTypes.Ncr, Guid.Empty, clock.Today(plant).ToDateTime(TimeOnly.MinValue), ct);   // per tenant: the number is also a Warehouse key
        var ncr = NonConformance.CreateManual(number, report, order.OrderNo, order.ItemId, order.ItemCode, order.ItemName, r.Quantity, r.Description, user.GetUserId());
        ncrs.Add(ncr);
        await uow.SaveChangesAsync(ct);
        return await NcrDto.BuildAsync(ncr, orders, ct);
    }
}

// ── Decide what happens to the quantity (BR-NCR-02 / 03) ────────────────────────────────────────────────────

public sealed record SaveNcrDispositionsCommand(Guid Id, IReadOnlyList<DispositionBody> Lines) : IRequest<NcrDto>;

public sealed class SaveNcrDispositionsHandler(
    IPlantAccessService access, INcrRepository ncrs, IProductionOrderRepository orders, IReasonCodeRepository reasons, IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<SaveNcrDispositionsCommand, NcrDto>
{
    public async Task<NcrDto> Handle(SaveNcrDispositionsCommand r, CancellationToken ct)
    {
        var ncr = await NcrScope.LoadAsync(ncrs, access, r.Id, ct);
        var lines = (r.Lines ?? []).Select(l => new DispositionInput(l.Type, l.Qty, l.ReasonCodeId, l.Note)).ToList();
        foreach (var scrap in lines.Where(l => l.Type == DispositionType.Scrap && l.ReasonCodeId is { } id && id != Guid.Empty))
        {
            var reason = await reasons.GetAsync(scrap.ReasonCodeId!.Value, ct);
            if (reason is not { IsActive: true, Type: ReasonType.Scrap }) throw MfgRules.Invalid("A Scrap line needs an active reason of type Scrap.");
        }
        ncr.SetDispositions(lines, user.HasPermission(PermissionConstants.Manufacturing_QcApproveDeviation), user.GetUserId(), clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return await NcrDto.BuildAsync(ncr, orders, ct);
    }
}

// ── Carry a line out ────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The optional choices of a Rework line: a routing, a due date, a priority.</summary>
public sealed record ExecuteDispositionBody(Guid? RoutingId, DateOnly? DueDate, int? Priority, string? Note);

public sealed record ExecuteNcrDispositionCommand(Guid Id, int LineNo, ExecuteDispositionBody? Options) : IRequest<NcrExecuteResultDto>;

public sealed record NcrExecuteResultDto(NcrDto Ncr, Guid? ReworkOrderId, string? ReworkOrderNo);

/// <summary>
/// Executes one disposition line. Warehouse effects are idempotent calls made BEFORE the line is marked (an outage is <c>MFG_INT_001</c>, nothing is marked, the person executes again):
/// Scrap = <c>ProductionScrap</c> of the held lot, UseAsIs = the part of the lot moves to the finished-goods warehouse and is released, Rework = a rework order is created (the line counts as
/// executed when that order is Completed). Output held by a failed in-process inspection (no stock) is scrapped or released on the order itself; a record-only NCR just notes the line done.
/// The NCR closes with its last line and settles the receipt line of its lot.
/// </summary>
public sealed class ExecuteNcrDispositionHandler(
    IPlantAccessService access, INcrRepository ncrs, IProductionOrderRepository orders, IWarehouseStockClient stock, IQuarantineRelease release, ReworkOrderFactory rework,
    IReasonCodeRepository reasons, INcrProgress progress, OperationLogCloser logCloser, IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<ExecuteNcrDispositionCommand, NcrExecuteResultDto>
{
    public async Task<NcrExecuteResultDto> Handle(ExecuteNcrDispositionCommand r, CancellationToken ct)
    {
        var ncr = await NcrScope.LoadAsync(ncrs, access, r.Id, ct);
        var line = ncr.EnsureExecutable(r.LineNo);
        var order = await orders.GetAsync(ncr.OrderId, ct) ?? throw new EntityNotFoundException(nameof(ProductionOrder), ncr.OrderId);
        var by = user.GetUserId();
        var now = clock.UtcNow;
        var key = $"{ncr.NcrNo}#{line.LineNo}";
        Guid? reworkOrderId = null;
        string? reworkOrderNo = null;

        if (ncr.IsRecordOnly)
        {
            ncr.MarkExecuted(line.LineNo, null, null, null, by, now);                                  // nothing moves: the finding is recorded and the line is ticked off
        }
        else if (ncr.IsInProcess)
        {
            var seq = ncr.OperationSeq!.Value;
            if (line.Type == DispositionType.Scrap) order.ApplyIpqcScrap(seq, line.Qty, by, now);
            else if (line.Type == DispositionType.UseAsIs) order.ApplyInspectionPassed(seq, line.Qty, by, now);
            else throw MfgRules.Fail(MfgErrors.Ncr003);
            await logCloser.CloseForCompletedAsync(order, ct);
            ncr.MarkExecuted(line.LineNo, null, null, null, by, now);
        }
        else
        {
            var plant = await access.GetVisiblePlantAsync(ncr.PlantId, ct);
            var lotId = ncr.LotId ?? throw MfgRules.Invalid($"{ncr.NcrNo} has no lot.");
            var lot = (await stock.ListLotsAsync(ncr.ItemId, ct)).FirstOrDefault(l => l.LotId == lotId) ?? throw MfgRules.Invalid($"Lot {ncr.LotNumber} no longer exists in the Warehouse.");
            var warehouse = lot.WarehouseId ?? plant.QuarantineWarehouseId;

            switch (line.Type)
            {
                case DispositionType.Scrap:
                {
                    var reasonName = line.ReasonCodeId is { } id ? (await reasons.GetAsync(id, ct))?.Name : null;
                    await stock.RecordMovementAsync(new("ProductionScrap", ncr.ItemId, line.Qty, key, WarehouseId: warehouse, LotId: lotId,
                        Reason: $"NCR {ncr.NcrNo}: scrap{(reasonName is null ? "" : " - " + reasonName)}", SourceOrderId: order.Id), ct);
                    ncr.MarkExecuted(line.LineNo, key, null, null, by, now);
                    break;
                }
                case DispositionType.UseAsIs:
                {
                    // QcRelease releases a whole lot: moving to the same warehouse would release more than the line, so a partial release needs the quarantine and finished-goods warehouses to differ
                    MfgRules.ThrowIf(warehouse == order.OutputWarehouseId && lot.RemainingQuantity > line.Qty, MfgErrors.Ncr005, ("lot", lot.LotNumber), ("qty", line.Qty), ("remaining", lot.RemainingQuantity));
                    var released = await release.ReleaseQuantityAsync(order, ncr.ItemId, lotId, warehouse, line.Qty, key, $"NCR {ncr.NcrNo}: use as is, approved by {line.ApprovedBy}", ct);
                    ncr.MarkExecuted(line.LineNo, key, released.LotId, string.IsNullOrEmpty(released.LotNumber) ? lot.LotNumber : released.LotNumber, by, now);
                    break;
                }
                default:
                {
                    MfgRules.ThrowIf(line.ReworkOrderId is not null, MfgErrors.Ncr004, ("ncr", ncr.NcrNo), ("reason", $"dòng {line.LineNo} đã có lệnh làm lại"));
                    MfgRules.ThrowIf(lot.RemainingQuantity < line.Qty, MfgErrors.Ncr005, ("lot", lot.LotNumber), ("qty", line.Qty), ("remaining", lot.RemainingQuantity));
                    var o = r.Options;
                    var created = await rework.CreateAsync(plant, ncr, line, order, lot, new ReworkOptions(o?.RoutingId, o?.DueDate, o?.Priority), by, ct);
                    ncr.MarkReworkStarted(line.LineNo, created.Id, by);
                    reworkOrderId = created.Id;
                    reworkOrderNo = created.OrderNo;
                    break;
                }
            }
        }

        if (ncr.Status == NcrStatus.Closed) await progress.SettleAsync(ncr, now, ct);
        await uow.SaveChangesAsync(ct);
        return new NcrExecuteResultDto(await NcrDto.BuildAsync(ncr, orders, ct), reworkOrderId, reworkOrderNo);
    }
}
