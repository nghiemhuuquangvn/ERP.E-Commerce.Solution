using ERP.MFG.Application.Features.Inspections;
using ERP.MFG.Application.Features.ItemProfiles;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Boms;

namespace ERP.MFG.UnitTests.Quality;

/// <summary>Inspection-plan helpers on real handlers over the in-memory stores; usable from every context that derives from <see cref="BomCtx"/>.</summary>
internal static class PlanHelpers
{
    public static Task<InspectionPlanDto> CreatePlanAsync(this BomCtx c, string item, string code, InspectionType type = InspectionType.Fqc, SamplingMethod sampling = SamplingMethod.Fixed,
        decimal? sample = 5, int minorAllowed = 1, DateOnly? from = null, DateOnly? to = null)
        => new CreateInspectionPlanHandler(c.Access, c.F.PlanRepo, c.F.ProductClient, c.F.Uow)
            .Handle(new CreateInspectionPlanCommand(c.Plant.Id, c.Items[item].Id, code, type, sampling, sample, minorAllowed, from ?? BomCtx.Jan1, to, null), default);

    public static InspectionCharacteristicBody Measure(string name, decimal? min, decimal? max, bool critical = false, int? seq = null)
        => new(seq, name, CharacteristicKind.Measurement, min, max, "mm", critical);

    public static InspectionCharacteristicBody Attribute(string name, bool critical = false, int? seq = null)
        => new(seq, name, CharacteristicKind.Attribute, null, null, null, critical);

    public static Task<InspectionPlanDto> AddCharAsync(this BomCtx c, Guid planId, InspectionCharacteristicBody body)
        => new AddInspectionCharacteristicHandler(c.Access, c.F.PlanRepo, c.F.Uow).Handle(new AddInspectionCharacteristicCommand(planId, body), default);

    public static Task<InspectionPlanDto> ApprovePlanAsync(this BomCtx c, Guid planId)
        => new ApproveInspectionPlanHandler(c.Access, c.F.PlanRepo, c.User, c.F.Uow).Handle(new ApproveInspectionPlanCommand(planId), default);

    public static Task<InspectionPlanDto> ObsoletePlanAsync(this BomCtx c, Guid planId, DateOnly? to = null)
        => new ObsoleteInspectionPlanHandler(c.Access, c.F.PlanRepo, c.F.Uow).Handle(new ObsoleteInspectionPlanCommand(planId, to), default);

    public static Task<InspectionPlanDto> CopyPlanAsync(this BomCtx c, Guid planId, DateOnly from)
        => new CopyInspectionPlanHandler(c.Access, c.F.PlanRepo, c.F.Uow).Handle(new CopyInspectionPlanCommand(planId, from), default);

    public static Task DeletePlanAsync(this BomCtx c, Guid planId)
        => new DeleteInspectionPlanHandler(c.Access, c.F.PlanRepo, c.F.References, c.F.Uow).Handle(new DeleteInspectionPlanCommand(planId), default);

    /// <summary>The FQC plan of the DS-01 top item as in TC-QC-007: C1 748..752 critical, C2 critical attribute, C3 and C4 minor, one minor defect tolerated, sample 5.</summary>
    public static async Task<InspectionPlanDto> SeedFqcPlanAsync(this BomCtx c, string code = "FQC-BAN01", bool approve = true)
    {
        var plan = await c.CreatePlanAsync("TP-BAN01", code);
        await c.AddCharAsync(plan.Id, Measure("Length", 748, 752, critical: true));
        await c.AddCharAsync(plan.Id, Attribute("Colour", critical: true));
        await c.AddCharAsync(plan.Id, Attribute("Scratches"));
        await c.AddCharAsync(plan.Id, Attribute("Label"));
        return approve ? await c.ApprovePlanAsync(plan.Id) : plan;
    }

    public static Task<ItemProfileDto> SaveProfileAsync(this BomCtx c, string item, ItemProfileValues values)
        => new UpsertItemProfileHandler(c.Access, c.F.ProfileRepo, c.F.ProductClient, c.F.PlanRepo, c.F.Uow).Handle(new UpsertItemProfileCommand(c.Items[item].Id, c.Plant.Id, values), default);
}
