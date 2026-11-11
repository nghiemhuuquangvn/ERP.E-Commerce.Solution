using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Routings;

public sealed record RoutingOperationDto(int OperationSeq, Guid? StandardOperationId, string Name, Guid WorkCenterId, string WorkCenterCode, decimal SetupTimeMinutes,
    decimal RunTimeMinutes, RunTimeBasis RunTimeBasis, decimal? BatchSize, int QueueTimeMinutes, int MoveTimeMinutes, bool IsSubcontract, string? Instructions, Guid? IpqcInspectionPlanId = null)
{
    public static RoutingOperationDto From(RoutingOperation o) => new(o.OperationSeq, o.StandardOperationId, o.Name, o.WorkCenterId, o.WorkCenterCode, o.SetupTimeMinutes,
        o.RunTimeMinutes, o.RunTimeBasis, o.BatchSize, o.QueueTimeMinutes, o.MoveTimeMinutes, o.IsSubcontract, o.Instructions, o.IpqcInspectionPlanId);
}

public sealed record RoutingDto(Guid Id, Guid PlantId, Guid ItemId, string ItemCode, string ItemName, int Version, VersionStatus Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    string? Description, string? ApprovedBy, DateTime? ApprovedAt, IReadOnlyList<RoutingOperationDto> Operations, IReadOnlyList<string> Warnings)
{
    public static RoutingDto From(Routing r, IReadOnlyList<string>? warnings = null) => new(r.Id, r.PlantId, r.ItemId, r.ItemCode, r.ItemName, r.Version, r.Status, r.EffectiveFrom,
        r.EffectiveTo, r.Description, r.ApprovedBy, r.ApprovedAt, r.Operations.Select(RoutingOperationDto.From).ToList(), warnings ?? []);
}

public sealed record RoutingListItemDto(Guid Id, Guid ItemId, string ItemCode, string ItemName, int Version, VersionStatus Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int OperationCount);

internal static class RoutingScope
{
    public static async Task<Routing> LoadAsync(IRoutingRepository repo, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var routing = await repo.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Routing), id);
        await access.GetVisiblePlantAsync(routing.PlantId, ct);
        return routing;
    }

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>The operation values of a request, completed from the standard operation when one is named (its values are copied, BR-OPS-01), and checked against the work center.</summary>
    public static async Task<RoutingOperationValues> BuildAsync(RoutingOperationBody b, Routing routing, IPlantCatalog catalog, CancellationToken ct)
    {
        StandardOperation? standard = null;
        if (b.StandardOperationId is { } sid)
        {
            standard = await catalog.GetAsync<StandardOperation>(sid, ct);
            if (standard is null || standard.PlantId != routing.PlantId) throw MfgRules.Invalid("The standard operation does not exist in this plant.");
        }

        var centerId = b.WorkCenterId ?? standard?.DefaultWorkCenterId ?? throw MfgRules.Invalid("A work center is required.");
        var center = await catalog.GetAsync<WorkCenter>(centerId, ct) ?? throw MfgRules.Fail(MfgErrors.Rt003);
        Routing.CheckWorkCenter(center, routing.PlantId, b.IsSubcontract);

        var basis = b.RunTimeBasis ?? standard?.RunTimeBasis ?? RunTimeBasis.PerUnit;
        return new RoutingOperationValues(
            b.OperationSeq ?? routing.NextSeq(), standard?.Id, string.IsNullOrWhiteSpace(b.Name) ? standard?.Name ?? $"Operation {b.OperationSeq ?? routing.NextSeq()}" : b.Name.Trim(), center.Id, center.Code,
            b.SetupTimeMinutes ?? standard?.SetupTimeMinutes ?? 0m, b.RunTimeMinutes ?? standard?.RunTimeMinutes ?? 0m, basis,
            basis == RunTimeBasis.PerBatch ? b.BatchSize ?? standard?.BatchSize : null, b.QueueTimeMinutes ?? center.QueueTimeMinutes, b.MoveTimeMinutes ?? center.MoveTimeMinutes,
            b.IsSubcontract, string.IsNullOrWhiteSpace(b.Instructions) ? standard?.Instructions : b.Instructions.Trim(), b.IpqcInspectionPlanId);
    }
}

/// <summary>An operation as a client sends it. With a standard operation, the fields left empty are copied from it; queue and move default to the work center's.</summary>
public sealed record RoutingOperationBody(int? OperationSeq, Guid? StandardOperationId, string? Name, Guid? WorkCenterId, decimal? SetupTimeMinutes, decimal? RunTimeMinutes,
    RunTimeBasis? RunTimeBasis, decimal? BatchSize, int? QueueTimeMinutes, int? MoveTimeMinutes, bool IsSubcontract, string? Instructions, Guid? IpqcInspectionPlanId = null);

// ── Draft ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CreateRoutingCommand(Guid PlantId, Guid ItemId, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, string? Description) : IRequest<RoutingDto>;

public sealed class CreateRoutingHandler(IPlantAccessService access, IRoutingRepository routings, IProductLookupClient products, IMfgUnitOfWork uow) : IRequestHandler<CreateRoutingCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(CreateRoutingCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var item = (await products.LookupAsync([r.ItemId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid($"Item {r.ItemId} does not exist.");
        if (!item.IsActive) throw MfgRules.Invalid($"Item {item.Sku} is not active.");

        var version = await routings.MaxVersionAsync(r.ItemId, plant.Id, ct) + 1;
        var routing = Routing.Create(r.ItemId, plant.Id, item.Sku, item.Name, version, r.EffectiveFrom ?? RoutingScope.Today(), r.EffectiveTo, r.Description);
        routings.Add(routing);
        await uow.SaveChangesAsync(ct);
        return RoutingDto.From(routing);
    }
}

public sealed record UpdateRoutingHeaderCommand(Guid Id, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Description) : IRequest<RoutingDto>;

public sealed class UpdateRoutingHeaderHandler(IPlantAccessService access, IRoutingRepository routings, IMfgUnitOfWork uow) : IRequestHandler<UpdateRoutingHeaderCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(UpdateRoutingHeaderCommand r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.Id, ct);
        routing.UpdateHeader(r.EffectiveFrom, r.EffectiveTo, r.Description);
        await uow.SaveChangesAsync(ct);
        return RoutingDto.From(routing);
    }
}

public sealed record DeleteRoutingCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteRoutingHandler(IPlantAccessService access, IRoutingRepository routings, IMfgUnitOfWork uow) : IRequestHandler<DeleteRoutingCommand, Unit>
{
    public async Task<Unit> Handle(DeleteRoutingCommand r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.Id, ct);
        routing.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record AddRoutingOperationCommand(Guid RoutingId, RoutingOperationBody Body) : IRequest<RoutingDto>;

public sealed class AddRoutingOperationHandler(IPlantAccessService access, IRoutingRepository routings, IPlantCatalog catalog, IInspectionPlanRepository plans, IMfgUnitOfWork uow) : IRequestHandler<AddRoutingOperationCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(AddRoutingOperationCommand r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.RoutingId, ct);
        routing.EnsureDraft();
        var values = await RoutingScope.BuildAsync(r.Body, routing, catalog, ct);
        await InspectionPlanPointer.CheckAsync(plans, values.IpqcInspectionPlanId, routing.PlantId, routing.ItemId, InspectionType.Ipqc, ct);
        routing.AddOperation(values);
        await uow.SaveChangesAsync(ct);
        return RoutingDto.From(routing);
    }
}

public sealed record UpdateRoutingOperationCommand(Guid RoutingId, int OperationSeq, RoutingOperationBody Body) : IRequest<RoutingDto>;

public sealed class UpdateRoutingOperationHandler(IPlantAccessService access, IRoutingRepository routings, IPlantCatalog catalog, IInspectionPlanRepository plans, IMfgUnitOfWork uow) : IRequestHandler<UpdateRoutingOperationCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(UpdateRoutingOperationCommand r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.RoutingId, ct);
        routing.EnsureDraft();
        var body = r.Body with { OperationSeq = r.Body.OperationSeq ?? r.OperationSeq };
        var values = await RoutingScope.BuildAsync(body, routing, catalog, ct);
        await InspectionPlanPointer.CheckAsync(plans, values.IpqcInspectionPlanId, routing.PlantId, routing.ItemId, InspectionType.Ipqc, ct);
        routing.UpdateOperation(r.OperationSeq, values);
        await uow.SaveChangesAsync(ct);
        return RoutingDto.From(routing);
    }
}

public sealed record RemoveRoutingOperationCommand(Guid RoutingId, int OperationSeq) : IRequest<RoutingDto>;

public sealed class RemoveRoutingOperationHandler(IPlantAccessService access, IRoutingRepository routings, IMfgUnitOfWork uow) : IRequestHandler<RemoveRoutingOperationCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(RemoveRoutingOperationCommand r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.RoutingId, ct);
        routing.RemoveOperation(r.OperationSeq);
        await uow.SaveChangesAsync(ct);
        return RoutingDto.From(routing);
    }
}

public sealed record RenumberRoutingCommand(Guid RoutingId, IReadOnlyList<int> OrderedSeqs) : IRequest<RoutingDto>;

public sealed class RenumberRoutingHandler(IPlantAccessService access, IRoutingRepository routings, IMfgUnitOfWork uow) : IRequestHandler<RenumberRoutingCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(RenumberRoutingCommand r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.RoutingId, ct);
        routing.ParkForRenumber(r.OrderedSeqs); // validates; the numbers are unique per routing, so the rows first leave their numbers
        await uow.SaveChangesAsync(ct);
        routing.Renumber(r.OrderedSeqs);
        await uow.SaveChangesAsync(ct);
        return RoutingDto.From(routing);
    }
}

// ── Life cycle ───────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Approval (BR-RT-03): operations still use active work centers of the plant, no overlap with another version (the open one before ends the day before),
/// then the BOM check (BR-RT-08) comes back as warnings: lines of the item's approved BOMs that name an operation this routing does not have.
/// </summary>
public sealed record ApproveRoutingCommand(Guid Id) : IRequest<RoutingDto>;

public sealed class ApproveRoutingHandler(IPlantAccessService access, IRoutingRepository routings, IBomRepository boms, IPlantCatalog catalog, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<ApproveRoutingCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(ApproveRoutingCommand r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.Id, ct);
        routing.EnsureDraft();

        var centers = (await catalog.GetManyAsync<WorkCenter>(routing.Operations.Select(o => o.WorkCenterId).Distinct().ToList(), ct)).ToDictionary(c => c.Id);
        foreach (var op in routing.Operations)
            Routing.CheckWorkCenter(centers.GetValueOrDefault(op.WorkCenterId) ?? throw MfgRules.Fail(MfgErrors.Rt003), routing.PlantId, op.IsSubcontract);

        var plan = DocumentVersioning.PlanApproval(routing, await routings.ListApprovedAsync(routing.PlantId, ct));
        DocumentVersioning.Apply(plan);
        routing.Approve(user.GetUserId(), DateTime.UtcNow);
        await uow.SaveChangesAsync(ct);

        var seqs = routing.Operations.Select(o => o.OperationSeq).ToList();
        var warnings = (await boms.ListApprovedAsync(routing.PlantId, ct)).Where(b => b.ItemId == routing.ItemId)
            .SelectMany(b => BomRoutingCheck.Warnings(b.Lines, seqs, routing.Version).Select(w => $"BOM version {b.Version}: {w}")).ToList();
        return RoutingDto.From(routing, warnings);
    }
}

public sealed record ObsoleteRoutingCommand(Guid Id, DateOnly? EffectiveTo) : IRequest<RoutingDto>;

public sealed class ObsoleteRoutingHandler(IPlantAccessService access, IRoutingRepository routings, IMfgUnitOfWork uow) : IRequestHandler<ObsoleteRoutingCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(ObsoleteRoutingCommand r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.Id, ct);
        if (routing.Status == VersionStatus.Obsolete && r.EffectiveTo is { } to) routing.SetEffectiveToOfObsolete(to);
        else routing.Obsolete(r.EffectiveTo ?? RoutingScope.Today());
        await uow.SaveChangesAsync(ct);
        return RoutingDto.From(routing);
    }
}

public sealed record CopyRoutingCommand(Guid Id, DateOnly? EffectiveFrom) : IRequest<RoutingDto>;

public sealed class CopyRoutingHandler(IPlantAccessService access, IRoutingRepository routings, IMfgUnitOfWork uow) : IRequestHandler<CopyRoutingCommand, RoutingDto>
{
    public async Task<RoutingDto> Handle(CopyRoutingCommand r, CancellationToken ct)
    {
        var source = await RoutingScope.LoadAsync(routings, access, r.Id, ct);
        var copy = source.CopyAsNewDraft(await routings.MaxVersionAsync(source.ItemId, source.PlantId, ct) + 1, r.EffectiveFrom ?? RoutingScope.Today());
        routings.Add(copy);
        await uow.SaveChangesAsync(ct);
        return RoutingDto.From(copy);
    }
}

// ── Reads ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetRoutingsQuery(Guid PlantId, Guid? ItemId, VersionStatus? Status) : IRequest<List<RoutingListItemDto>>;

public sealed class GetRoutingsHandler(IPlantAccessService access, IRoutingRepository routings) : IRequestHandler<GetRoutingsQuery, List<RoutingListItemDto>>
{
    public async Task<List<RoutingListItemDto>> Handle(GetRoutingsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return (await routings.ListAsync(plant.Id, r.ItemId, r.Status, ct)).OrderBy(x => x.ItemCode).ThenByDescending(x => x.Version)
            .Select(x => new RoutingListItemDto(x.Id, x.ItemId, x.ItemCode, x.ItemName, x.Version, x.Status, x.EffectiveFrom, x.EffectiveTo, x.Operations.Count)).ToList();
    }
}

public sealed record GetRoutingQuery(Guid Id) : IRequest<RoutingDto>;

public sealed class GetRoutingHandler(IPlantAccessService access, IRoutingRepository routings) : IRequestHandler<GetRoutingQuery, RoutingDto>
{
    public async Task<RoutingDto> Handle(GetRoutingQuery r, CancellationToken ct) => RoutingDto.From(await RoutingScope.LoadAsync(routings, access, r.Id, ct));
}

/// <summary>The routing that applies to an item on a date; an item may have none (BR-RT-07), then 404.</summary>
public sealed record GetEffectiveRoutingQuery(Guid PlantId, Guid ItemId, DateOnly? Date) : IRequest<RoutingDto>;

public sealed class GetEffectiveRoutingHandler(IPlantAccessService access, IRoutingResolver resolver) : IRequestHandler<GetEffectiveRoutingQuery, RoutingDto>
{
    public async Task<RoutingDto> Handle(GetEffectiveRoutingQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var routing = await resolver.GetEffectiveRoutingAsync(r.ItemId, plant.Id, r.Date ?? RoutingScope.Today(), ct) ?? throw new EntityNotFoundException(nameof(Routing), r.ItemId);
        return RoutingDto.From(routing);
    }
}

public sealed record GetRoutingLeadTimeQuery(Guid RoutingId, decimal Quantity, DateOnly EndDate) : IRequest<RoutingLeadTime>;

public sealed class GetRoutingLeadTimeHandler(IPlantAccessService access, IRoutingRepository routings, IOperationTimeService time) : IRequestHandler<GetRoutingLeadTimeQuery, RoutingLeadTime>
{
    public async Task<RoutingLeadTime> Handle(GetRoutingLeadTimeQuery r, CancellationToken ct)
    {
        var routing = await RoutingScope.LoadAsync(routings, access, r.RoutingId, ct);
        return await time.LeadTimeAsync(routing, r.Quantity, r.EndDate, ct);
    }
}
