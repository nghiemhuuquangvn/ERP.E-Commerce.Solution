using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Inspections;

public sealed record InspectionCharacteristicDto(Guid Id, int Seq, string Name, CharacteristicKind Kind, decimal? MinValue, decimal? MaxValue, string? Unit, bool IsCritical)
{
    public static InspectionCharacteristicDto From(InspectionCharacteristic c) => new(c.Id, c.Seq, c.Name, c.Kind, c.MinValue, c.MaxValue, c.Unit, c.IsCritical);
}

public sealed record InspectionPlanDto(Guid Id, Guid PlantId, string Code, InspectionType Type, Guid ItemId, string ItemCode, string ItemName, int Version, VersionStatus Status,
    SamplingMethod SamplingMethod, decimal? SampleValue, int AllowedMinorDefects, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Description, string? ApprovedBy, DateTime? ApprovedAt,
    IReadOnlyList<InspectionCharacteristicDto> Characteristics)
{
    public static InspectionPlanDto From(InspectionPlan p) => new(p.Id, p.PlantId, p.Code, p.Type, p.ItemId, p.ItemCode, p.ItemName, p.Version, p.Status, p.SamplingMethod, p.SampleValue,
        p.AllowedMinorDefects, p.EffectiveFrom, p.EffectiveTo, p.Description, p.ApprovedBy, p.ApprovedAt, p.Characteristics.Select(InspectionCharacteristicDto.From).ToList());
}

public sealed record InspectionPlanListItemDto(Guid Id, string Code, InspectionType Type, Guid ItemId, string ItemCode, string ItemName, int Version, VersionStatus Status,
    SamplingMethod SamplingMethod, decimal? SampleValue, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int CharacteristicCount);

/// <summary>A characteristic as a client sends it; the sequence defaults to the next free one (steps of 10).</summary>
public sealed record InspectionCharacteristicBody(int? Seq, string? Name, CharacteristicKind Kind, decimal? MinValue, decimal? MaxValue, string? Unit, bool IsCritical);

internal static class PlanScope
{
    public static async Task<InspectionPlan> LoadAsync(IInspectionPlanRepository repo, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var plan = await repo.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(InspectionPlan), id);
        await access.GetVisiblePlantAsync(plan.PlantId, ct);
        return plan;
    }

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    public static InspectionCharacteristicValues Values(InspectionCharacteristicBody b, InspectionPlan plan, int? seq = null)
        => new(b.Seq ?? seq ?? plan.NextSeq(), MasterCode.Name(b.Name, 200), b.Kind, b.MinValue, b.MaxValue, string.IsNullOrWhiteSpace(b.Unit) ? null : b.Unit.Trim(), b.IsCritical);
}

// ── Draft ────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>A new plan series (version 1, Draft). The code names the series and cannot be used twice in a plant; new versions come from Copy.</summary>
public sealed record CreateInspectionPlanCommand(Guid PlantId, Guid ItemId, string Code, InspectionType Type, SamplingMethod SamplingMethod, decimal? SampleValue, int AllowedMinorDefects,
    DateOnly? EffectiveFrom, DateOnly? EffectiveTo, string? Description) : IRequest<InspectionPlanDto>;

public sealed class CreateInspectionPlanHandler(IPlantAccessService access, IInspectionPlanRepository plans, IProductLookupClient products, IMfgUnitOfWork uow)
    : IRequestHandler<CreateInspectionPlanCommand, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(CreateInspectionPlanCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var item = (await products.LookupAsync([r.ItemId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid($"Item {r.ItemId} does not exist.");
        if (!item.IsActive) throw MfgRules.Invalid($"Item {item.Sku} is not active.");

        var code = MasterCode.Valid(r.Code);
        if (await plans.MaxVersionAsync(plant.Id, code, ct) > 0) throw MfgRules.Fail(MfgErrors.Gen006, ("code", code));

        var plan = InspectionPlan.Create(plant.Id, item.Id, item.Sku, item.Name, code, r.Type, 1, r.SamplingMethod, r.SampleValue, r.AllowedMinorDefects,
            r.EffectiveFrom ?? PlanScope.Today(), r.EffectiveTo, r.Description);
        plans.Add(plan);
        await uow.SaveChangesAsync(ct);
        return InspectionPlanDto.From(plan);
    }
}

public sealed record UpdateInspectionPlanHeaderCommand(Guid Id, SamplingMethod SamplingMethod, decimal? SampleValue, int AllowedMinorDefects, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Description)
    : IRequest<InspectionPlanDto>;

public sealed class UpdateInspectionPlanHeaderHandler(IPlantAccessService access, IInspectionPlanRepository plans, IMfgUnitOfWork uow) : IRequestHandler<UpdateInspectionPlanHeaderCommand, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(UpdateInspectionPlanHeaderCommand r, CancellationToken ct)
    {
        var plan = await PlanScope.LoadAsync(plans, access, r.Id, ct);
        plan.UpdateHeader(r.SamplingMethod, r.SampleValue, r.AllowedMinorDefects, r.EffectiveFrom, r.EffectiveTo, r.Description);
        await uow.SaveChangesAsync(ct);
        return InspectionPlanDto.From(plan);
    }
}

public sealed record DeleteInspectionPlanCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteInspectionPlanHandler(IPlantAccessService access, IInspectionPlanRepository plans, IReferenceChecker references, IMfgUnitOfWork uow) : IRequestHandler<DeleteInspectionPlanCommand, Unit>
{
    public async Task<Unit> Handle(DeleteInspectionPlanCommand r, CancellationToken ct)
    {
        var plan = await PlanScope.LoadAsync(plans, access, r.Id, ct);
        plan.EnsureDraft();
        if (await references.InspectionPlanIsUsedAsync(plan.Id, ct)) throw MfgRules.Fail(MfgErrors.Gen005);
        plan.MarkDeleted();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record AddInspectionCharacteristicCommand(Guid PlanId, InspectionCharacteristicBody Body) : IRequest<InspectionPlanDto>;

public sealed class AddInspectionCharacteristicHandler(IPlantAccessService access, IInspectionPlanRepository plans, IMfgUnitOfWork uow) : IRequestHandler<AddInspectionCharacteristicCommand, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(AddInspectionCharacteristicCommand r, CancellationToken ct)
    {
        var plan = await PlanScope.LoadAsync(plans, access, r.PlanId, ct);
        plan.EnsureDraft();
        plan.AddCharacteristic(PlanScope.Values(r.Body, plan));
        await uow.SaveChangesAsync(ct);
        return InspectionPlanDto.From(plan);
    }
}

public sealed record UpdateInspectionCharacteristicCommand(Guid PlanId, int Seq, InspectionCharacteristicBody Body) : IRequest<InspectionPlanDto>;

public sealed class UpdateInspectionCharacteristicHandler(IPlantAccessService access, IInspectionPlanRepository plans, IMfgUnitOfWork uow) : IRequestHandler<UpdateInspectionCharacteristicCommand, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(UpdateInspectionCharacteristicCommand r, CancellationToken ct)
    {
        var plan = await PlanScope.LoadAsync(plans, access, r.PlanId, ct);
        plan.EnsureDraft();
        plan.UpdateCharacteristic(r.Seq, PlanScope.Values(r.Body, plan, r.Seq));
        await uow.SaveChangesAsync(ct);
        return InspectionPlanDto.From(plan);
    }
}

public sealed record RemoveInspectionCharacteristicCommand(Guid PlanId, int Seq) : IRequest<InspectionPlanDto>;

public sealed class RemoveInspectionCharacteristicHandler(IPlantAccessService access, IInspectionPlanRepository plans, IMfgUnitOfWork uow) : IRequestHandler<RemoveInspectionCharacteristicCommand, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(RemoveInspectionCharacteristicCommand r, CancellationToken ct)
    {
        var plan = await PlanScope.LoadAsync(plans, access, r.PlanId, ct);
        plan.RemoveCharacteristic(r.Seq);
        await uow.SaveChangesAsync(ct);
        return InspectionPlanDto.From(plan);
    }
}

// ── Life cycle ───────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>BR-QC-01: only an Approved version is used. Versions of one series must not overlap (the open one before ends the day before the new one starts).</summary>
public sealed record ApproveInspectionPlanCommand(Guid Id) : IRequest<InspectionPlanDto>;

public sealed class ApproveInspectionPlanHandler(IPlantAccessService access, IInspectionPlanRepository plans, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<ApproveInspectionPlanCommand, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(ApproveInspectionPlanCommand r, CancellationToken ct)
    {
        var plan = await PlanScope.LoadAsync(plans, access, r.Id, ct);
        plan.EnsureDraft();
        var siblings = (await plans.ListSeriesAsync(plan.PlantId, plan.Code, ct)).Where(p => p.Status == VersionStatus.Approved).ToList();
        DocumentVersioning.Apply(DocumentVersioning.PlanApproval(plan, siblings));
        plan.Approve(user.GetUserId(), DateTime.UtcNow);
        await uow.SaveChangesAsync(ct);
        return InspectionPlanDto.From(plan);
    }
}

public sealed record ObsoleteInspectionPlanCommand(Guid Id, DateOnly? EffectiveTo) : IRequest<InspectionPlanDto>;

public sealed class ObsoleteInspectionPlanHandler(IPlantAccessService access, IInspectionPlanRepository plans, IMfgUnitOfWork uow) : IRequestHandler<ObsoleteInspectionPlanCommand, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(ObsoleteInspectionPlanCommand r, CancellationToken ct)
    {
        var plan = await PlanScope.LoadAsync(plans, access, r.Id, ct);
        if (plan.Status == VersionStatus.Obsolete && r.EffectiveTo is { } to) plan.SetEffectiveToOfObsolete(to);
        else plan.Obsolete(r.EffectiveTo ?? PlanScope.Today());
        await uow.SaveChangesAsync(ct);
        return InspectionPlanDto.From(plan);
    }
}

public sealed record CopyInspectionPlanCommand(Guid Id, DateOnly? EffectiveFrom) : IRequest<InspectionPlanDto>;

public sealed class CopyInspectionPlanHandler(IPlantAccessService access, IInspectionPlanRepository plans, IMfgUnitOfWork uow) : IRequestHandler<CopyInspectionPlanCommand, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(CopyInspectionPlanCommand r, CancellationToken ct)
    {
        var source = await PlanScope.LoadAsync(plans, access, r.Id, ct);
        var copy = source.CopyAsNewDraft(await plans.MaxVersionAsync(source.PlantId, source.Code, ct) + 1, r.EffectiveFrom ?? PlanScope.Today());
        plans.Add(copy);
        await uow.SaveChangesAsync(ct);
        return InspectionPlanDto.From(copy);
    }
}

// ── Reads ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetInspectionPlansQuery(Guid PlantId, InspectionType? Type, Guid? ItemId, VersionStatus? Status) : IRequest<List<InspectionPlanListItemDto>>;

public sealed class GetInspectionPlansHandler(IPlantAccessService access, IInspectionPlanRepository plans) : IRequestHandler<GetInspectionPlansQuery, List<InspectionPlanListItemDto>>
{
    public async Task<List<InspectionPlanListItemDto>> Handle(GetInspectionPlansQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return (await plans.ListAsync(plant.Id, r.Type, r.ItemId, r.Status, ct)).OrderBy(p => p.ItemCode).ThenBy(p => p.Code).ThenByDescending(p => p.Version)
            .Select(p => new InspectionPlanListItemDto(p.Id, p.Code, p.Type, p.ItemId, p.ItemCode, p.ItemName, p.Version, p.Status, p.SamplingMethod, p.SampleValue, p.EffectiveFrom, p.EffectiveTo,
                p.Characteristics.Count)).ToList();
    }
}

public sealed record GetInspectionPlanQuery(Guid Id) : IRequest<InspectionPlanDto>;

public sealed class GetInspectionPlanHandler(IPlantAccessService access, IInspectionPlanRepository plans) : IRequestHandler<GetInspectionPlanQuery, InspectionPlanDto>
{
    public async Task<InspectionPlanDto> Handle(GetInspectionPlanQuery r, CancellationToken ct) => InspectionPlanDto.From(await PlanScope.LoadAsync(plans, access, r.Id, ct));
}
