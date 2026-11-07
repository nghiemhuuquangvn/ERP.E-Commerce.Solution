using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Boms;

public sealed record BomLineDto(int LineNo, BomLineType LineType, Guid ComponentItemId, string ComponentCode, string ComponentName, decimal Quantity, Guid UomId,
    decimal ScrapPct, bool IsFixedQuantity, bool IsPhantom, IssueMethod IssueMethod, int? OperationSeq, string? AlternativeGroup, int? AlternativePriority,
    decimal? CostAllocationPct, string? Notes)
{
    public static BomLineDto From(BomLine l) => new(l.LineNo, l.LineType, l.ComponentItemId, l.ComponentCode, l.ComponentName, l.Quantity, l.UomId, l.ScrapPct,
        l.IsFixedQuantity, l.IsPhantom, l.IssueMethod, l.OperationSeq, l.AlternativeGroup, l.AlternativePriority, l.CostAllocationPct, l.Notes);
}

public sealed record BomDto(Guid Id, Guid PlantId, Guid ItemId, string ItemCode, string ItemName, int Version, decimal BaseQuantity, decimal YieldPct, VersionStatus Status,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Description, string? ApprovedBy, DateTime? ApprovedAt, IReadOnlyList<BomLineDto> Lines, IReadOnlyList<string> Warnings)
{
    public static BomDto From(Bom b, IReadOnlyList<string>? warnings = null) => new(b.Id, b.PlantId, b.ItemId, b.ItemCode, b.ItemName, b.Version, b.BaseQuantity, b.YieldPct,
        b.Status, b.EffectiveFrom, b.EffectiveTo, b.Description, b.ApprovedBy, b.ApprovedAt, b.Lines.Select(BomLineDto.From).ToList(), warnings ?? []);
}

public sealed record BomListItemDto(Guid Id, Guid ItemId, string ItemCode, string ItemName, int Version, VersionStatus Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int LineCount);

internal static class BomScope
{
    /// <summary>A BOM of a plant the caller may see; otherwise it does not exist for them (404, BR-GEN-09).</summary>
    public static async Task<Bom> LoadAsync(IBomRepository repo, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var bom = await repo.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Bom), id);
        await access.GetVisiblePlantAsync(bom.PlantId, ct);
        return bom;
    }

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>BR-BOM-13: rewrite the low-level code of every item of the plant from the approved BOMs given (items that are in no BOM go back to 0).</summary>
    public static async Task RewriteLowLevelCodesAsync(IItemProfileRepository profiles, Guid plantId, IEnumerable<Bom> approved, CancellationToken ct)
    {
        var codes = BomGraph.LowLevelCodes(BomGraph.EdgesOf(approved));
        foreach (var profile in await profiles.ListByPlantAsync(plantId, ct))
            profile.SetLowLevelCode(codes.GetValueOrDefault(profile.ItemId, 0));
    }
}

// ── Create / edit a draft ────────────────────────────────────────────────────────────────────────────────────

public sealed record CreateBomCommand(Guid PlantId, Guid ItemId, decimal? BaseQuantity, decimal? YieldPct, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, string? Description) : IRequest<BomDto>;

public sealed class CreateBomHandler(IPlantAccessService access, IBomRepository boms, IItemProfileRepository profiles, IProductLookupClient products, IMfgUnitOfWork uow)
    : IRequestHandler<CreateBomCommand, BomDto>
{
    public async Task<BomDto> Handle(CreateBomCommand r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var item = (await products.LookupAsync([r.ItemId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid($"Item {r.ItemId} does not exist.");
        if (!item.IsActive) throw MfgRules.Invalid($"Item {item.Sku} is not active.");

        // BR-BOM-01: only an item that is made can have a BOM. An item with no profile counts as made (the documented default).
        var profile = await profiles.GetAsync(r.ItemId, plant.Id, ct);
        if (profile is not null && profile.MakeOrBuy == MakeOrBuy.Buy) throw MfgRules.Fail(MfgErrors.Bom001);

        var version = await boms.MaxVersionAsync(r.ItemId, plant.Id, ct) + 1;
        var bom = Bom.Create(r.ItemId, plant.Id, item.Sku, item.Name, version, r.BaseQuantity ?? 1m, r.YieldPct ?? 100m,
            r.EffectiveFrom ?? BomScope.Today(), r.EffectiveTo, r.Description);
        boms.Add(bom);
        await uow.SaveChangesAsync(ct);
        return BomDto.From(bom);
    }
}

public sealed record UpdateBomHeaderCommand(Guid Id, decimal BaseQuantity, decimal YieldPct, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Description) : IRequest<BomDto>;

public sealed class UpdateBomHeaderHandler(IPlantAccessService access, IBomRepository boms, IMfgUnitOfWork uow) : IRequestHandler<UpdateBomHeaderCommand, BomDto>
{
    public async Task<BomDto> Handle(UpdateBomHeaderCommand r, CancellationToken ct)
    {
        var bom = await BomScope.LoadAsync(boms, access, r.Id, ct);
        bom.UpdateHeader(r.BaseQuantity, r.YieldPct, r.EffectiveFrom, r.EffectiveTo, r.Description);
        await uow.SaveChangesAsync(ct);
        return BomDto.From(bom);
    }
}

public sealed record DeleteBomCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteBomHandler(IPlantAccessService access, IBomRepository boms, IMfgUnitOfWork uow) : IRequestHandler<DeleteBomCommand, Unit>
{
    public async Task<Unit> Handle(DeleteBomCommand r, CancellationToken ct)
    {
        var bom = await BomScope.LoadAsync(boms, access, r.Id, ct);
        bom.MarkDeleted(); // Draft only (BR-BOM-02)
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>The values of a line as a client sends them; the component's code and name come from ProductService, never from the request.</summary>
public sealed record BomLineBody(BomLineType LineType, Guid ComponentItemId, decimal Quantity, Guid UomId, decimal ScrapPct, bool IsFixedQuantity, bool IsPhantom,
    IssueMethod IssueMethod, int? OperationSeq, string? AlternativeGroup, int? AlternativePriority, decimal? CostAllocationPct, string? Notes);

internal static class BomLineFactory
{
    public static async Task<BomLineValues> ToValuesAsync(BomLineBody b, IBomSnapshotFactory snapshots, CancellationToken ct)
    {
        var component = await snapshots.ResolveComponentAsync(b.ComponentItemId, b.UomId, ct); // BR-BOM-01, BR-BOM-07
        var group = string.IsNullOrWhiteSpace(b.AlternativeGroup) ? null : b.AlternativeGroup.Trim().ToUpperInvariant();
        return new BomLineValues(b.LineType, component.ItemId, component.Code, component.Name, b.Quantity, b.UomId, b.ScrapPct, b.IsFixedQuantity, b.IsPhantom,
            b.IssueMethod, b.OperationSeq, group, b.AlternativePriority, b.CostAllocationPct,
            string.IsNullOrWhiteSpace(b.Notes) ? null : b.Notes.Trim());
    }
}

public sealed record AddBomLineCommand(Guid BomId, BomLineBody Body) : IRequest<BomDto>;

public sealed class AddBomLineHandler(IPlantAccessService access, IBomRepository boms, IBomSnapshotFactory snapshots, IMfgUnitOfWork uow) : IRequestHandler<AddBomLineCommand, BomDto>
{
    public async Task<BomDto> Handle(AddBomLineCommand r, CancellationToken ct)
    {
        var bom = await BomScope.LoadAsync(boms, access, r.BomId, ct);
        bom.EnsureDraft(); // before any call to another service
        bom.AddLine(await BomLineFactory.ToValuesAsync(r.Body, snapshots, ct));
        await uow.SaveChangesAsync(ct);
        return BomDto.From(bom);
    }
}

public sealed record UpdateBomLineCommand(Guid BomId, int LineNo, BomLineBody Body) : IRequest<BomDto>;

public sealed class UpdateBomLineHandler(IPlantAccessService access, IBomRepository boms, IBomSnapshotFactory snapshots, IMfgUnitOfWork uow) : IRequestHandler<UpdateBomLineCommand, BomDto>
{
    public async Task<BomDto> Handle(UpdateBomLineCommand r, CancellationToken ct)
    {
        var bom = await BomScope.LoadAsync(boms, access, r.BomId, ct);
        bom.EnsureDraft();
        bom.UpdateLine(r.LineNo, await BomLineFactory.ToValuesAsync(r.Body, snapshots, ct));
        await uow.SaveChangesAsync(ct);
        return BomDto.From(bom);
    }
}

public sealed record RemoveBomLineCommand(Guid BomId, int LineNo) : IRequest<BomDto>;

public sealed class RemoveBomLineHandler(IPlantAccessService access, IBomRepository boms, IMfgUnitOfWork uow) : IRequestHandler<RemoveBomLineCommand, BomDto>
{
    public async Task<BomDto> Handle(RemoveBomLineCommand r, CancellationToken ct)
    {
        var bom = await BomScope.LoadAsync(boms, access, r.BomId, ct);
        bom.RemoveLine(r.LineNo);
        await uow.SaveChangesAsync(ct);
        return BomDto.From(bom);
    }
}

// ── Life cycle ───────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Approval (BR-BOM-02 .. -04, -11, -13): structure, parent still made, no overlap with another version (the open one before is closed the day before),
/// no cycle over all approved BOMs and this one (the path is in the message), depth within <c>MaxBomLevels</c>, and the low-level codes of the plant are rewritten -
/// all saved together, so there is never a half-approved state.
/// </summary>
public sealed record ApproveBomCommand(Guid Id) : IRequest<BomDto>;

public sealed class ApproveBomHandler(IPlantAccessService access, IBomRepository boms, IItemProfileRepository profiles, IProductionSettingProvider settings,
    IRoutingRepository routings, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<ApproveBomCommand, BomDto>
{
    public async Task<BomDto> Handle(ApproveBomCommand r, CancellationToken ct)
    {
        var bom = await BomScope.LoadAsync(boms, access, r.Id, ct);
        bom.EnsureDraft();
        bom.ValidateForApproval();

        var profile = await profiles.GetAsync(bom.ItemId, bom.PlantId, ct);
        if (profile is not null && profile.MakeOrBuy == MakeOrBuy.Buy) throw MfgRules.Fail(MfgErrors.Bom001);

        var approved = (await boms.ListApprovedAsync(bom.PlantId, ct)).Where(b => b.Id != bom.Id).ToList();
        var plan = DocumentVersioning.PlanApproval(bom, approved);

        var all = approved.Append(bom).ToList();
        var edges = BomGraph.EdgesOf(all);
        if (BomGraph.FindCycle(edges, bom.ItemId) is { } cycle)
        {
            var labels = all.SelectMany(b => b.Lines.Select(l => (l.ComponentItemId, l.ComponentCode))).Concat(all.Select(b => (b.ItemId, b.ItemCode)))
                .GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.First().Item2);
            throw MfgRules.Fail(MfgErrors.Bom004, ("path", string.Join(" > ", cycle.Select(i => labels.GetValueOrDefault(i, i.ToString())))));
        }

        var maxLevels = (await settings.GetAsync(bom.PlantId, ct)).MaxBomLevels;
        MfgRules.ThrowIf(BomGraph.LowLevelCodes(edges).Values.DefaultIfEmpty(0).Max() > maxLevels, MfgErrors.Bom008, ("max", maxLevels));

        DocumentVersioning.Apply(plan);
        bom.Approve(user.GetUserId(), DateTime.UtcNow);
        await BomScope.RewriteLowLevelCodesAsync(profiles, bom.PlantId, all, ct);
        await uow.SaveChangesAsync(ct);

        // BR-RT-08: the operations the lines name must exist in the routing that applies (warning only; no routing = nothing to check, BR-RT-07)
        var routing = DocumentVersioning.Resolve((await routings.ListApprovedAsync(bom.PlantId, ct)).Where(x => x.ItemId == bom.ItemId), bom.EffectiveFrom);
        var warnings = BomRoutingCheck.Warnings(bom.Lines, routing?.Operations.Select(o => o.OperationSeq).ToList(), routing?.Version ?? 0);
        return BomDto.From(bom, warnings);
    }
}

public sealed record ObsoleteBomCommand(Guid Id, DateOnly? EffectiveTo) : IRequest<BomDto>;

public sealed class ObsoleteBomHandler(IPlantAccessService access, IBomRepository boms, IItemProfileRepository profiles, IMfgUnitOfWork uow) : IRequestHandler<ObsoleteBomCommand, BomDto>
{
    public async Task<BomDto> Handle(ObsoleteBomCommand r, CancellationToken ct)
    {
        var bom = await BomScope.LoadAsync(boms, access, r.Id, ct);
        if (bom.Status == VersionStatus.Obsolete && r.EffectiveTo is { } to) // the one change an obsolete version allows
        {
            bom.SetEffectiveToOfObsolete(to);
        }
        else
        {
            bom.Obsolete(r.EffectiveTo ?? BomScope.Today());
            var stillApproved = (await boms.ListApprovedAsync(bom.PlantId, ct)).Where(b => b.Id != bom.Id && b.Status == VersionStatus.Approved);
            await BomScope.RewriteLowLevelCodesAsync(profiles, bom.PlantId, stillApproved, ct);
        }
        await uow.SaveChangesAsync(ct);
        return BomDto.From(bom);
    }
}

public sealed record CopyBomCommand(Guid Id, DateOnly? EffectiveFrom) : IRequest<BomDto>;

public sealed class CopyBomHandler(IPlantAccessService access, IBomRepository boms, IMfgUnitOfWork uow) : IRequestHandler<CopyBomCommand, BomDto>
{
    public async Task<BomDto> Handle(CopyBomCommand r, CancellationToken ct)
    {
        var source = await BomScope.LoadAsync(boms, access, r.Id, ct);
        var version = await boms.MaxVersionAsync(source.ItemId, source.PlantId, ct) + 1; // BR-BOM-15
        var copy = source.CopyAsNewDraft(version, r.EffectiveFrom ?? BomScope.Today());
        boms.Add(copy);
        await uow.SaveChangesAsync(ct);
        return BomDto.From(copy);
    }
}

// ── Reads ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetBomsQuery(Guid PlantId, Guid? ItemId, VersionStatus? Status) : IRequest<List<BomListItemDto>>;

public sealed class GetBomsHandler(IPlantAccessService access, IBomRepository boms) : IRequestHandler<GetBomsQuery, List<BomListItemDto>>
{
    public async Task<List<BomListItemDto>> Handle(GetBomsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return (await boms.ListAsync(plant.Id, r.ItemId, r.Status, ct))
            .OrderBy(b => b.ItemCode).ThenByDescending(b => b.Version)
            .Select(b => new BomListItemDto(b.Id, b.ItemId, b.ItemCode, b.ItemName, b.Version, b.Status, b.EffectiveFrom, b.EffectiveTo, b.Lines.Count)).ToList();
    }
}

public sealed record GetBomQuery(Guid Id) : IRequest<BomDto>;

public sealed class GetBomHandler(IPlantAccessService access, IBomRepository boms) : IRequestHandler<GetBomQuery, BomDto>
{
    public async Task<BomDto> Handle(GetBomQuery r, CancellationToken ct) => BomDto.From(await BomScope.LoadAsync(boms, access, r.Id, ct));
}

/// <summary>The BOM that applies to an item on a date (T2.3.1), 404 when there is none.</summary>
public sealed record GetEffectiveBomQuery(Guid PlantId, Guid ItemId, DateOnly? Date) : IRequest<BomDto>;

public sealed class GetEffectiveBomHandler(IPlantAccessService access, IBomResolver resolver) : IRequestHandler<GetEffectiveBomQuery, BomDto>
{
    public async Task<BomDto> Handle(GetEffectiveBomQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var date = r.Date ?? BomScope.Today();
        var bom = await resolver.GetEffectiveBomAsync(r.ItemId, plant.Id, date, ct) ?? throw MfgRules.Fail(MfgErrors.Po004, ("date", date.ToString("yyyy-MM-dd")));
        return BomDto.From(bom);
    }
}

public sealed record ExplodeBomQuery(Guid PlantId, Guid ItemId, Guid? BomId, decimal Quantity, DateOnly? Date) : IRequest<ExplosionResult>;

public sealed class ExplodeBomHandler(IPlantAccessService access, IBomExplosionService explosion) : IRequestHandler<ExplodeBomQuery, ExplosionResult>
{
    public async Task<ExplosionResult> Handle(ExplodeBomQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return await explosion.ExplodeAsync(plant.Id, r.ItemId, r.BomId, r.Quantity, r.Date ?? BomScope.Today(), ct);
    }
}

public sealed record IndentedBomQuery(Guid PlantId, Guid ItemId, Guid? BomId, decimal Quantity, DateOnly? Date) : IRequest<IndentedNode>;

public sealed class IndentedBomHandler(IPlantAccessService access, IBomExplosionService explosion) : IRequestHandler<IndentedBomQuery, IndentedNode>
{
    public async Task<IndentedNode> Handle(IndentedBomQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        return await explosion.IndentedAsync(plant.Id, r.ItemId, r.BomId, r.Quantity, r.Date ?? BomScope.Today(), ct);
    }
}

public sealed record WhereUsedQuery(Guid PlantId, Guid ItemId) : IRequest<IReadOnlyList<BomGraph.WhereUsedRow>>;

public sealed class WhereUsedHandler(IPlantAccessService access, IBomRepository boms, IProductionSettingProvider settings) : IRequestHandler<WhereUsedQuery, IReadOnlyList<BomGraph.WhereUsedRow>>
{
    public async Task<IReadOnlyList<BomGraph.WhereUsedRow>> Handle(WhereUsedQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var maxLevels = (await settings.GetAsync(plant.Id, ct)).MaxBomLevels;
        return BomGraph.WhereUsed(await boms.ListApprovedAsync(plant.Id, ct), r.ItemId, maxLevels);
    }
}

public sealed record CompareBomsQuery(Guid LeftId, Guid RightId) : IRequest<IReadOnlyList<BomDifference>>;

public sealed class CompareBomsHandler(IPlantAccessService access, IBomRepository boms) : IRequestHandler<CompareBomsQuery, IReadOnlyList<BomDifference>>
{
    public async Task<IReadOnlyList<BomDifference>> Handle(CompareBomsQuery r, CancellationToken ct)
    {
        var left = await BomScope.LoadAsync(boms, access, r.LeftId, ct);
        var right = await BomScope.LoadAsync(boms, access, r.RightId, ct);
        if (left.ItemId != right.ItemId || left.PlantId != right.PlantId) throw MfgRules.Invalid("Only two versions of the same item can be compared.");
        return BomComparer.Compare(left, right);
    }
}

public sealed record LowLevelCodeDto(Guid ItemId, string ItemCode, int LowLevelCode);

/// <summary>The low-level code of every item that is in an approved BOM, computed now (the stored code exists only for items with a profile).</summary>
public sealed record GetLowLevelCodesQuery(Guid PlantId) : IRequest<List<LowLevelCodeDto>>;

public sealed class GetLowLevelCodesHandler(IPlantAccessService access, IBomRepository boms) : IRequestHandler<GetLowLevelCodesQuery, List<LowLevelCodeDto>>
{
    public async Task<List<LowLevelCodeDto>> Handle(GetLowLevelCodesQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var approved = await boms.ListApprovedAsync(plant.Id, ct);
        var labels = approved.SelectMany(b => b.Lines.Select(l => (l.ComponentItemId, l.ComponentCode))).Concat(approved.Select(b => (b.ItemId, b.ItemCode)))
            .GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.First().Item2);
        return BomGraph.LowLevelCodes(BomGraph.EdgesOf(approved)).Select(kv => new LowLevelCodeDto(kv.Key, labels.GetValueOrDefault(kv.Key, string.Empty), kv.Value))
            .OrderBy(x => x.LowLevelCode).ThenBy(x => x.ItemCode).ToList();
    }
}
