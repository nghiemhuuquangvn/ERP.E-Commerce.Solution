using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using MediatR;

namespace ERP.MFG.Application.Features.Orders;

/// <summary>
/// The answer of a transition that touches the reservation: the order, the materials the Warehouse cannot cover in full (BR-PO-07) and whether the
/// Warehouse reservation still has to catch up (it could not be reached or refused; the sync job keeps trying).
/// </summary>
public sealed record OrderActionResultDto(OrderDto Order, IReadOnlyList<MaterialAvailability> Shortages, bool ReservationPending);

public sealed record OrderAvailabilityDto(Guid OrderId, bool HasShortage, IReadOnlyList<MaterialAvailability> Materials);

internal static class OrderTransitions
{
    /// <summary>Saves the order with its new state and its sync flag, then lets the Warehouse catch up. A failed catch-up is a visible state, not an error.</summary>
    public static async Task<OrderActionResultDto> SaveAndSyncAsync(ProductionOrder order, IMfgUnitOfWork uow, IOrderReservationSync sync, IReadOnlyList<MaterialAvailability>? shortages, CancellationToken ct)
    {
        await uow.SaveChangesAsync(ct);
        var result = await sync.SyncAsync(order, ct);
        return new OrderActionResultDto(OrderDto.From(order), shortages ?? [], !result.InSync || order.ReservationSyncPending);
    }

    /// <summary>A reason must be chosen (<c>MFG_PO_009</c>), exist, be active and be of the right kind.</summary>
    public static async Task EnsureReasonAsync(IReasonCodeRepository reasons, Guid? id, ReasonType type, CancellationToken ct)
    {
        if (id is null || id == Guid.Empty) throw MfgRules.Fail(MfgErrors.Po009);
        var reason = await reasons.GetAsync(id.Value, ct);
        if (reason is null || reason.Type != type || !reason.IsActive) throw MfgRules.Fail(MfgErrors.Po009);
    }
}

// ── Availability ─────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetOrderAvailabilityQuery(Guid Id) : IRequest<OrderAvailabilityDto>;

public sealed class GetOrderAvailabilityHandler(IPlantAccessService access, IProductionOrderRepository orders, IOrderAvailability availability)
    : IRequestHandler<GetOrderAvailabilityQuery, OrderAvailabilityDto>
{
    public async Task<OrderAvailabilityDto> Handle(GetOrderAvailabilityQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        var result = await availability.CheckAsync(order, ct);
        return new OrderAvailabilityDto(order.Id, result.HasShortage, result.Materials);
    }
}

// ── Release (BR-PO-07 / -08) ───────────────────────────────────────────────────────────────────────────────

public sealed record ReleaseOrderCommand(Guid Id) : IRequest<OrderActionResultDto>;

public sealed class ReleaseOrderHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IProductLookupClient products, IPlantCatalog catalog, IProductionSettingProvider settings,
    IOrderAvailability availability, IOrderReservationSync sync, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<ReleaseOrderCommand, OrderActionResultDto>
{
    public async Task<OrderActionResultDto> Handle(ReleaseOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        order.EnsureDraft();
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        MfgRules.ThrowIf(!plant.IsActive, MfgErrors.Po006, ("code", plant.Code));

        // the item, the components and the work centers are still usable (BR-PO-07)
        var ids = order.Materials.Select(m => m.ComponentItemId).Append(order.ItemId).Distinct().ToList();
        var known = (await products.LookupAsync(ids, ct)).ToDictionary(p => p.Id);
        foreach (var id in ids)
        {
            if (!known.TryGetValue(id, out var product)) throw MfgRules.Invalid($"Item {id} does not exist any more.");
            MfgRules.ThrowIf(!product.IsActive, MfgErrors.Po006, ("code", product.Sku));
            MfgRules.ThrowIf(product.IsSerialControlled, MfgErrors.Mat011, ("item", product.Sku));
        }
        var centers = (await catalog.GetManyAsync<WorkCenter>(order.Operations.Select(o => o.WorkCenterId).Distinct().ToList(), ct)).ToDictionary(c => c.Id);
        foreach (var op in order.Operations)
            MfgRules.ThrowIf(!centers.TryGetValue(op.WorkCenterId, out var center) || !center.IsActive, MfgErrors.Po006, ("code", op.WorkCenterCode));

        // availability decides a block (fail closed when the Warehouse cannot be read)
        var check = await availability.CheckAsync(order, ct);
        var setting = await settings.GetAsync(plant.Id, ct);
        MfgRules.ThrowIf(setting.BlockReleaseOnShortage && check.HasShortage, MfgErrors.Po007);

        order.Release(setting.RequireSequentialOperations, user.GetUserId());
        return await OrderTransitions.SaveAndSyncAsync(order, uow, sync, check.Shortages, ct);
    }
}

// ── Unrelease (BR-PO-09) ───────────────────────────────────────────────────────────────────────────────────

public sealed record UnreleaseOrderCommand(Guid Id) : IRequest<OrderActionResultDto>;

public sealed class UnreleaseOrderHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IOrderTransactionGuard transactions, IOrderReservationSync sync, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<UnreleaseOrderCommand, OrderActionResultDto>
{
    public async Task<OrderActionResultDto> Handle(UnreleaseOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        MfgRules.ThrowIf(order.Status == OrderStatus.Released && await transactions.HasTransactionsAsync(order, ct), MfgErrors.Po008);
        order.Unrelease(user.GetUserId());
        return await OrderTransitions.SaveAndSyncAsync(order, uow, sync, null, ct);
    }
}

// ── Cancel (BR-PO-13) ──────────────────────────────────────────────────────────────────────────────────────

public sealed record CancelOrderCommand(Guid Id, Guid? ReasonCodeId, string? Note) : IRequest<OrderActionResultDto>;

public sealed class CancelOrderHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IReasonCodeRepository reasons, IOrderTransactionGuard transactions, IOrderReservationSync sync,
    IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<CancelOrderCommand, OrderActionResultDto>
{
    public async Task<OrderActionResultDto> Handle(CancelOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        await OrderTransitions.EnsureReasonAsync(reasons, r.ReasonCodeId, ReasonType.Cancel, ct);
        MfgRules.ThrowIf(order.Status == OrderStatus.Released && await transactions.HasTransactionsAsync(order, ct), MfgErrors.Po008);
        order.Cancel(r.ReasonCodeId, r.Note, user.GetUserId());
        return await OrderTransitions.SaveAndSyncAsync(order, uow, sync, null, ct);
    }
}

// ── Hold / Resume (BR-PO-11) ───────────────────────────────────────────────────────────────────────────────

public sealed record HoldOrderCommand(Guid Id, Guid? ReasonCodeId, string? Note) : IRequest<OrderActionResultDto>;

public sealed class HoldOrderHandler(IPlantAccessService access, IProductionOrderRepository orders, IReasonCodeRepository reasons, ITimeLogRepository logs, IPlantClock clock,
    IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<HoldOrderCommand, OrderActionResultDto>
{
    public async Task<OrderActionResultDto> Handle(HoldOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        await OrderTransitions.EnsureReasonAsync(reasons, r.ReasonCodeId, ReasonType.Hold, ct);
        order.Hold(r.ReasonCodeId, r.Note, user.GetUserId());             // running operations pause here ...
        var now = clock.UtcNow;
        foreach (var log in (await logs.ListOpenForOrderAsync(order.Id, ct)).Where(l => l.IsRunning)) log.Pause(now);   // ... and so do their time logs (BR-PO-11)
        await uow.SaveChangesAsync(ct);                                  // the reservation stays: nothing to sync
        return new OrderActionResultDto(OrderDto.From(order), [], order.ReservationSyncPending);
    }
}

public sealed record ResumeOrderCommand(Guid Id) : IRequest<OrderActionResultDto>;

public sealed class ResumeOrderHandler(IPlantAccessService access, IProductionOrderRepository orders, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<ResumeOrderCommand, OrderActionResultDto>
{
    public async Task<OrderActionResultDto> Handle(ResumeOrderCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        order.Resume(user.GetUserId());
        await uow.SaveChangesAsync(ct);
        return new OrderActionResultDto(OrderDto.From(order), [], order.ReservationSyncPending);
    }
}

// ── Change of quantity (BR-PO-12) ──────────────────────────────────────────────────────────────────────────

public sealed record ChangeOrderQuantityCommand(Guid Id, decimal Quantity, string? Note) : IRequest<OrderActionResultDto>;

public sealed class ChangeOrderQuantityHandler(
    IPlantAccessService access, IProductionOrderRepository orders, OrderSnapshotBuilder builder, IOrderReservationSync sync, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<ChangeOrderQuantityCommand, OrderActionResultDto>
{
    public async Task<OrderActionResultDto> Handle(ChangeOrderQuantityCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.Id, ct);
        order.EnsureEditable();
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);

        var recomputed = await builder.RecomputeForQuantityAsync(plant, order, r.Quantity, ct);
        order.ChangeQuantity(r.Quantity, recomputed.RequiredByLine, recomputed.PlannedByOutputLine, recomputed.MinutesBySeq, recomputed.Schedule, r.Note, user.GetUserId());
        return await OrderTransitions.SaveAndSyncAsync(order, uow, sync, null, ct);
    }
}
