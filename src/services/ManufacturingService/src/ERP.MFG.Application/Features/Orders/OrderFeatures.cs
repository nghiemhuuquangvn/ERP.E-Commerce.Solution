using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Orders;

public sealed record OrderMaterialDto(int LineNo, Guid ComponentItemId, string ComponentCode, string ComponentName, Guid UomId, int? OperationSeq, decimal RequiredQty,
    decimal IssuedQty, decimal ReturnedQty, decimal ConsumedQty, decimal ReservedQty, decimal ScrapPct, IssueMethod IssueMethod, Guid WarehouseId, bool IsSubstitute,
    string? AlternativeGroup, bool IsUnplanned)
{
    public static OrderMaterialDto From(ProductionOrderMaterial m) => new(m.LineNo, m.ComponentItemId, m.ComponentCode, m.ComponentName, m.UomId, m.OperationSeq, m.RequiredQty,
        m.IssuedQty, m.ReturnedQty, m.ConsumedQty, m.ReservedQty, m.ScrapPct, m.IssueMethod, m.WarehouseId, m.IsSubstitute, m.AlternativeGroup, m.IsUnplanned);
}

public sealed record OrderOperationDto(int OperationSeq, string Name, Guid WorkCenterId, string WorkCenterCode, decimal SetupTimeMinutes, decimal RunTimeMinutes,
    RunTimeBasis RunTimeBasis, decimal? BatchSize, int QueueTimeMinutes, int MoveTimeMinutes, bool IsSubcontract, OperationStatus Status, decimal StandardMinutes,
    int CalendarMinutes, DateTime? PlannedStart, DateTime? PlannedEnd, DateTime? ActualStart, DateTime? ActualEnd, decimal GoodQty, decimal ScrapQty, decimal ReworkQty)
{
    public static OrderOperationDto From(ProductionOrderOperation o) => new(o.OperationSeq, o.Name, o.WorkCenterId, o.WorkCenterCode, o.SetupTimeMinutes, o.RunTimeMinutes,
        o.RunTimeBasis, o.BatchSize, o.QueueTimeMinutes, o.MoveTimeMinutes, o.IsSubcontract, o.Status, o.StandardMinutes, o.CalendarMinutes, o.PlannedStart, o.PlannedEnd,
        o.ActualStart, o.ActualEnd, o.GoodQty, o.ScrapQty, o.ReworkQty);
}

public sealed record OrderOutputDto(int LineNo, BomLineType LineType, Guid ItemId, string ItemCode, string ItemName, Guid UomId, decimal PlannedQty, decimal ReceivedQty, decimal? CostAllocationPct)
{
    public static OrderOutputDto From(ProductionOrderOutput o) => new(o.LineNo, o.LineType, o.ItemId, o.ItemCode, o.ItemName, o.UomId, o.PlannedQty, o.ReceivedQty, o.CostAllocationPct);
}

public sealed record OrderDto(
    Guid Id, string OrderNo, Guid PlantId, OrderType OrderType, Guid ItemId, string ItemCode, string ItemName, decimal Quantity, Guid UomId, Guid BomId, Guid? RoutingId,
    OrderStatus Status, OrderStatus? StatusBeforeHold, int Priority, DateOnly DueDate, DateTime PlannedStart, DateTime PlannedEnd, DateTime? ActualStart, DateTime? ActualEnd,
    bool IsLate, Guid OutputWarehouseId, OrderSourceType SourceType, Guid? SourceRefId, Guid? ParentOrderId, decimal CompletedQty, decimal ScrappedQty, bool IsRework, string? Notes,
    Guid? ReservationId, bool ReservationSyncPending, string? ReservationSyncError, IReadOnlyList<OrderMaterialDto> Materials, IReadOnlyList<OrderOperationDto> Operations, IReadOnlyList<OrderOutputDto> Outputs, IReadOnlyList<string> Warnings, int PendingStockDocuments = 0)
{
    public static OrderDto From(ProductionOrder o, IReadOnlyList<string>? warnings = null) => new(
        o.Id, o.OrderNo, o.PlantId, o.OrderType, o.ItemId, o.ItemCode, o.ItemName, o.Quantity, o.UomId, o.BomId, o.RoutingId, o.Status, o.StatusBeforeHold, o.Priority, o.DueDate,
        o.PlannedStart, o.PlannedEnd, o.ActualStart, o.ActualEnd, o.IsLate, o.OutputWarehouseId, o.SourceType, o.SourceRefId, o.ParentOrderId, o.CompletedQty, o.ScrappedQty,
        o.IsRework, o.Notes, o.ReservationId, o.ReservationSyncPending, o.ReservationSyncError, o.Materials.Select(OrderMaterialDto.From).ToList(), o.Operations.Select(OrderOperationDto.From).ToList(),
        o.Outputs.Select(OrderOutputDto.From).ToList(), warnings ?? []);
}

public sealed record OrderListItemDto(Guid Id, string OrderNo, Guid ItemId, string ItemCode, string ItemName, decimal Quantity, decimal CompletedQty, OrderStatus Status, int Priority,
    DateOnly DueDate, DateTime PlannedStart, DateTime PlannedEnd, bool IsLate)
{
    public static OrderListItemDto From(ProductionOrder o) => new(o.Id, o.OrderNo, o.ItemId, o.ItemCode, o.ItemName, o.Quantity, o.CompletedQty, o.Status, o.Priority, o.DueDate,
        o.PlannedStart, o.PlannedEnd, o.IsLate);
}

public sealed record OrderPageDto(IReadOnlyList<OrderListItemDto> Items, int Total, int Page, int PageSize);

public sealed record OrderHistoryDto(OrderStatus FromStatus, OrderStatus ToStatus, Guid? ReasonCodeId, string? Note, string ChangedBy, DateTime ChangedAt)
{
    public static OrderHistoryDto From(ProductionOrderStatusHistory h) => new(h.FromStatus, h.ToStatus, h.ReasonCodeId, h.Note, h.ChangedBy, h.ChangedAt);
}

internal static class OrderScope
{
    /// <summary>An order of a plant the caller may see; otherwise it does not exist for them (404, BR-GEN-09).</summary>
    public static async Task<ProductionOrder> LoadAsync(IProductionOrderRepository repo, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var order = await repo.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(ProductionOrder), id);
        await access.GetVisiblePlantAsync(order.PlantId, ct);
        return order;
    }
}

// ── Create ───────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CreateOrderCommand(
    Guid PlantId, Guid ItemId, decimal Quantity, DateOnly DueDate, int? Priority, Guid? BomId, Guid? RoutingId, Guid? OutputWarehouseId, string? Notes) : IRequest<OrderDto>;

public sealed class CreateOrderHandler(
    IPlantAccessService access, OrderSnapshotBuilder builder, IProductionOrderRepository orders, IDocumentNumberService numbers, IPlantClock clock,
    IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        MfgRules.ThrowIf(!plant.IsActive, MfgErrors.Po006, ("code", plant.Code));

        var built = await builder.BuildAsync(new OrderBuildRequest(plant, r.ItemId, r.Quantity, r.DueDate, r.BomId, r.RoutingId, r.OutputWarehouseId, clock.Today(plant)), ct);
        var orderNo = await numbers.NextAsync(DocumentTypes.ProductionOrder, Guid.Empty, clock.LocalNow(plant), ct); // one counter per tenant and month: the number is also the key the Warehouse deduplicates on

        var header = new OrderHeaderValues(plant.Id, r.ItemId, built.Item.Sku, built.Item.Name, r.Quantity, built.UomId, built.BomId, built.RoutingId, r.Priority ?? 3, r.DueDate,
            built.OutputWarehouseId, OrderType.Standard, OrderSourceType.Manual, null, null, r.Notes);
        var order = ProductionOrder.Create(orderNo, header, built.Snapshot, built.Schedule, user.GetUserId());
        orders.Add(order);
        await uow.SaveChangesAsync(ct);
        return OrderDto.From(order, built.Warnings);
    }
}

// ── Change a Draft ──────────────────────────────────────────────────────────────────────────────────────────

/// <summary>BR-PO-05: quantity, BOM or routing of a Draft changed -> a fresh snapshot; due date, priority, output warehouse or notes only -> same lines, new schedule.</summary>
public sealed record UpdateOrderCommand(Guid Id, decimal Quantity, DateOnly DueDate, int? Priority, Guid? BomId, Guid? RoutingId, Guid? OutputWarehouseId, string? Notes) : IRequest<OrderDto>;

public sealed class UpdateOrderHandler(
    IPlantAccessService access, OrderSnapshotBuilder builder, IProductionOrderRepository orders, IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<UpdateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(UpdateOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        order.EnsureDraft();
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);

        // the version used so far stays unless the caller names another one (Regenerate keeps the plan-start date of the old schedule as the reference day)
        var oldStart = DateOnly.FromDateTime(clock.ToLocal(plant, order.PlannedStart));
        var asOf = oldStart > clock.Today(plant) ? oldStart : clock.Today(plant);
        var built = await builder.BuildAsync(new OrderBuildRequest(plant, order.ItemId, r.Quantity, r.DueDate, r.BomId ?? order.BomId, r.RoutingId ?? order.RoutingId, r.OutputWarehouseId ?? order.OutputWarehouseId, asOf), ct);

        var changes = new OrderDraftChanges(r.Quantity, r.DueDate, r.Priority ?? order.Priority, built.BomId, built.RoutingId, built.OutputWarehouseId, r.Notes);
        var sameLines = r.Quantity == order.Quantity && built.BomId == order.BomId && built.RoutingId == order.RoutingId;
        if (sameLines) order.Reschedule(changes, built.Schedule, user.GetUserId());
        else order.Regenerate(changes, built.Snapshot, built.Schedule, user.GetUserId());

        await uow.SaveChangesAsync(ct);
        return OrderDto.From(order, built.Warnings);
    }
}

// ── Read ─────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetOrdersQuery(Guid PlantId, OrderStatus? Status, Guid? ItemId, DateOnly? DueFrom, DateOnly? DueTo, bool? IsLate, int? Priority, string? Search, int Page, int PageSize)
    : IRequest<OrderPageDto>;

public sealed class GetOrdersHandler(IPlantAccessService access, IProductionOrderRepository orders) : IRequestHandler<GetOrdersQuery, OrderPageDto>
{
    public async Task<OrderPageDto> Handle(GetOrdersQuery r, CancellationToken ct)
    {
        await access.GetVisiblePlantAsync(r.PlantId, ct);
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 200);
        var (items, total) = await orders.ListAsync(new OrderFilter(r.PlantId, r.Status, r.ItemId, r.DueFrom, r.DueTo, r.IsLate, r.Priority, r.Search), page, size, ct);
        return new OrderPageDto(items.Select(OrderListItemDto.From).ToList(), total, page, size);
    }
}

public sealed record GetOrderQuery(Guid Id) : IRequest<OrderDto>;

public sealed class GetOrderHandler(IPlantAccessService access, IProductionOrderRepository orders, IStockDocumentRepository? documents = null) : IRequestHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        var dto = OrderDto.From(order);
        return documents is null ? dto : dto with { PendingStockDocuments = await documents.CountPendingAsync(order.Id, ct) };
    }
}

public sealed record GetOrderHistoryQuery(Guid Id) : IRequest<IReadOnlyList<OrderHistoryDto>>;

public sealed class GetOrderHistoryHandler(IPlantAccessService access, IProductionOrderRepository orders) : IRequestHandler<GetOrderHistoryQuery, IReadOnlyList<OrderHistoryDto>>
{
    public async Task<IReadOnlyList<OrderHistoryDto>> Handle(GetOrderHistoryQuery r, CancellationToken ct)
        => (await OrderScope.LoadAsync(orders, access, r.Id, ct)).History.Select(OrderHistoryDto.From).ToList();
}
