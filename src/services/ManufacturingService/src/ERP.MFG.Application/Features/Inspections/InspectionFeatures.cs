using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Inspections;

// ── IPQC / FQC inspections (BR-QC-02..06), P4-3 ──────────────────────────────────────────────────────────────

public sealed record InspectionResultDto(Guid CharacteristicId, int SampleNo, decimal? MeasuredValue, bool? AttributePassed, bool IsPassed, string? Note)
{
    public static InspectionResultDto From(InspectionResult r) => new(r.CharacteristicId, r.SampleNo, r.MeasuredValue, r.AttributePassed, r.IsPassed, r.Note);
}

public sealed record InspectionListItemDto(Guid Id, string DocNo, InspectionType Type, InspectionStatus Status, Guid OrderId, string OrderNo, int? OperationSeq, Guid ItemId, string ItemCode,
    string ItemName, Guid? LotId, string? LotNumber, decimal LotQty, decimal SampleQty, string PlanCode, int PlanVersion, Guid? ReceiptDocumentId, Guid? ReceiptLineId, Guid? OperationReportId,
    string? InspectedBy, DateTime? InspectedAt, bool NcrRequired, Guid? ReinspectionOfId, Guid? ReinspectedById, DateTime? ReleasedAt, DateTime CreatedAt)
{
    public static InspectionListItemDto From(Inspection i) => new(i.Id, i.DocNo, i.Type, i.Status, i.OrderId, i.OrderNo, i.OperationSeq, i.ItemId, i.ItemCode, i.ItemName, i.LotId, i.LotNumber,
        i.LotQty, i.SampleQty, i.PlanCode, i.PlanVersion, i.ReceiptDocumentId, i.ReceiptLineId, i.OperationReportId, i.InspectedBy, i.InspectedAt, i.NcrRequired, i.ReinspectionOfId,
        i.ReinspectedById, i.ReleasedAt, i.CreatedAt);
}

public sealed record PagedInspections(List<InspectionListItemDto> Items, int Total, int Page, int PageSize);

/// <summary>An inspection with the plan version it uses (what to check) and the values entered so far. <see cref="Units"/> = how many values a measurement needs.</summary>
public sealed record InspectionDto(InspectionListItemDto Inspection, SamplingMethod SamplingMethod, decimal? SampleValue, int AllowedMinorDefects, int Units,
    IReadOnlyList<InspectionCharacteristicDto> Characteristics, IReadOnlyList<InspectionResultDto> Results)
{
    public static InspectionDto From(Inspection i, InspectionPlan plan) => new(InspectionListItemDto.From(i), plan.SamplingMethod, plan.SampleValue, plan.AllowedMinorDefects,
        QualityRules.Units(i.SampleQty), plan.Characteristics.Select(InspectionCharacteristicDto.From).ToList(), i.Results.Select(InspectionResultDto.From).ToList());
}

/// <summary>A value typed by an inspector: a measurement of sample unit <c>SampleNo</c>, or the pass / fail (<c>Passed</c>, sample 1) of an attribute.</summary>
public sealed record InspectionResultBody(Guid CharacteristicId, int SampleNo, decimal? Value, bool? Passed, string? Note);

internal static class InspectionScope
{
    public static async Task<Inspection> LoadAsync(IInspectionRepository repo, IPlantAccessService access, Guid id, CancellationToken ct)
    {
        var inspection = await repo.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Inspection), id);
        await access.GetVisiblePlantAsync(inspection.PlantId, ct);
        return inspection;
    }

    public static async Task<InspectionPlan> PlanOfAsync(IInspectionPlanRepository plans, Inspection inspection, CancellationToken ct)
        => await plans.GetAsync(inspection.PlanId, ct) ?? throw new EntityNotFoundException(nameof(InspectionPlan), inspection.PlanId);

    public static IEnumerable<InspectionResultInput> Inputs(IEnumerable<InspectionResultBody>? bodies)
        => (bodies ?? []).Select(b => new InspectionResultInput(b.CharacteristicId, b.SampleNo, b.Value, b.Passed, b.Note));
}

// ── Reads ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetInspectionsQuery(Guid PlantId, InspectionType? Type, InspectionStatus? Status, Guid? OrderId, Guid? ItemId, string? Search, int Page = 1, int PageSize = 50) : IRequest<PagedInspections>;

public sealed class GetInspectionsHandler(IPlantAccessService access, IInspectionRepository inspections) : IRequestHandler<GetInspectionsQuery, PagedInspections>
{
    public async Task<PagedInspections> Handle(GetInspectionsQuery r, CancellationToken ct)
    {
        var plant = await access.GetVisiblePlantAsync(r.PlantId, ct);
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 200);
        var (items, total) = await inspections.ListAsync(new InspectionFilter(plant.Id, r.Type, r.Status, r.OrderId, r.ItemId, r.Search), page, size, ct);
        return new PagedInspections(items.Select(InspectionListItemDto.From).ToList(), total, page, size);
    }
}

public sealed record GetInspectionQuery(Guid Id) : IRequest<InspectionDto>;

public sealed class GetInspectionHandler(IPlantAccessService access, IInspectionRepository inspections, IInspectionPlanRepository plans) : IRequestHandler<GetInspectionQuery, InspectionDto>
{
    public async Task<InspectionDto> Handle(GetInspectionQuery r, CancellationToken ct)
    {
        var inspection = await InspectionScope.LoadAsync(inspections, access, r.Id, ct);
        return InspectionDto.From(inspection, await InspectionScope.PlanOfAsync(plans, inspection, ct));
    }
}

public sealed record GetOrderInspectionsQuery(Guid OrderId) : IRequest<List<InspectionListItemDto>>;

public sealed class GetOrderInspectionsHandler(IPlantAccessService access, IProductionOrderRepository orders, IInspectionRepository inspections) : IRequestHandler<GetOrderInspectionsQuery, List<InspectionListItemDto>>
{
    public async Task<List<InspectionListItemDto>> Handle(GetOrderInspectionsQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        return (await inspections.ListForOrderAsync(order.Id, ct)).Select(InspectionListItemDto.From).ToList();
    }
}

// ── Enter values and submit ───────────────────────────────────────────────────────────────────────────────────

/// <summary>Saves values as a draft; the inspection stays Pending (BR-QC-06 makes it final only when submitted).</summary>
public sealed record SaveInspectionResultsCommand(Guid Id, decimal? SampleQty, IReadOnlyList<InspectionResultBody> Results) : IRequest<InspectionDto>;

public sealed class SaveInspectionResultsHandler(IPlantAccessService access, IInspectionRepository inspections, IInspectionPlanRepository plans, IMfgUnitOfWork uow)
    : IRequestHandler<SaveInspectionResultsCommand, InspectionDto>
{
    public async Task<InspectionDto> Handle(SaveInspectionResultsCommand r, CancellationToken ct)
    {
        var inspection = await InspectionScope.LoadAsync(inspections, access, r.Id, ct);
        inspection.EnsurePending();                                                // MFG_QC_002
        inspection.CheckSample(r.SampleQty);                                       // MFG_QC_001
        var plan = await InspectionScope.PlanOfAsync(plans, inspection, ct);
        inspection.SaveDraft(plan, InspectionScope.Inputs(r.Results));
        await uow.SaveChangesAsync(ct);
        return InspectionDto.From(inspection, plan);
    }
}

/// <summary>
/// The inspector submits (values may travel with it): the verdict is evaluated first (<c>MFG_QC_003</c> while values are missing), a Pass of a final inspection releases the lot in the
/// Warehouse BEFORE the result is saved (Warehouse unreachable = <c>MFG_INT_001</c>, nothing saved, the values stay as a draft and the person submits again; the Warehouse calls are
/// idempotent), a Pass of an in-process inspection frees its Good quantity for the next operation, a Fail keeps the hold and asks for a non-conformance report.
/// </summary>
public sealed record SubmitInspectionCommand(Guid Id, decimal? SampleQty, IReadOnlyList<InspectionResultBody>? Results) : IRequest<InspectionDto>;

public sealed class SubmitInspectionHandler(
    IPlantAccessService access, IInspectionRepository inspections, IInspectionPlanRepository plans, IProductionOrderRepository orders, IStockDocumentRepository documents,
    IQuarantineRelease release, OperationLogCloser logCloser, IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<SubmitInspectionCommand, InspectionDto>
{
    public async Task<InspectionDto> Handle(SubmitInspectionCommand r, CancellationToken ct)
    {
        var inspection = await InspectionScope.LoadAsync(inspections, access, r.Id, ct);
        inspection.EnsurePending();                                                // MFG_QC_002
        inspection.CheckSample(r.SampleQty);                                       // MFG_QC_001
        var plan = await InspectionScope.PlanOfAsync(plans, inspection, ct);
        if (r.Results is { Count: > 0 }) inspection.SaveDraft(plan, InspectionScope.Inputs(r.Results));
        var outcome = inspection.Evaluate(plan);                                   // MFG_QC_003

        var order = await orders.GetAsync(inspection.OrderId, ct) ?? throw new EntityNotFoundException(nameof(ProductionOrder), inspection.OrderId);
        var by = user.GetUserId();
        var now = clock.UtcNow;

        if (outcome.Passed && inspection.IsFqc)
        {
            var document = await documents.GetAsync(inspection.ReceiptDocumentId ?? Guid.Empty, ct) ?? throw new EntityNotFoundException(nameof(MaterialTransaction), inspection.ReceiptDocumentId ?? Guid.Empty);
            var line = document.ReceiptLines.FirstOrDefault(l => l.Id == inspection.ReceiptLineId) ?? throw MfgRules.Invalid($"The receipt line of {inspection.DocNo} no longer exists.");
            if (line.QcReleasedAt is null)
            {
                await release.ReleaseAsync(order, line, $"Final inspection {inspection.DocNo} passed", ct);   // idempotent keys Q1 / Q2: a second submit after an outage finishes the job
                document.RecordQcReleased(line, now);
            }
            inspection.MarkReleased(now);
        }
        else if (outcome.Passed && inspection.OperationSeq is { } seq)
        {
            order.ApplyInspectionPassed(seq, inspection.LotQty, by, now);          // the Good quantity is free for the next operation
            await logCloser.CloseForCompletedAsync(order, ct);
        }

        inspection.Complete(outcome, by, now);
        await uow.SaveChangesAsync(ct);
        return InspectionDto.From(inspection, plan);
    }
}

/// <summary>BR-QC-06: a Failed inspection is repeated by a new document that points back at it. Only a Failed one, only once.</summary>
public sealed record ReinspectCommand(Guid Id) : IRequest<InspectionDto>;

public sealed class ReinspectHandler(
    IPlantAccessService access, IInspectionRepository inspections, IInspectionPlanRepository plans, IDocumentNumberService numbers, IPlantClock clock, IMfgUnitOfWork uow)
    : IRequestHandler<ReinspectCommand, InspectionDto>
{
    public async Task<InspectionDto> Handle(ReinspectCommand r, CancellationToken ct)
    {
        var failed = await InspectionScope.LoadAsync(inspections, access, r.Id, ct);
        var plant = await access.GetVisiblePlantAsync(failed.PlantId, ct);
        var docNo = await numbers.NextAsync(DocumentTypes.Inspection, plant.Id, clock.Today(plant).ToDateTime(TimeOnly.MinValue), ct);
        var again = failed.CreateReinspection(docNo);
        inspections.Add(again);
        await uow.SaveChangesAsync(ct);
        return InspectionDto.From(again, await InspectionScope.PlanOfAsync(plans, again, ct));
    }
}
