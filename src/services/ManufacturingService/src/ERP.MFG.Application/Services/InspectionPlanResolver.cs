using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Services;

public sealed class InspectionPlanResolver(IInspectionPlanRepository plans) : IInspectionPlanResolver
{
    public async Task<InspectionPlan?> ResolveAsync(Guid? planId, DateOnly date, CancellationToken ct = default)
    {
        if (planId is not { } id) return null;
        var pointed = await plans.GetAsync(id, ct);
        if (pointed is null) return null;
        return DocumentVersioning.Resolve(await plans.ListSeriesAsync(pointed.PlantId, pointed.Code, ct), date);
    }
}

/// <summary>
/// The rule for a pointer to a plan on a profile or an operation (P4-3): the series must exist, belong to the same plant, have the right type and be for the same item
/// (an FQC plan for the item itself; an IPQC plan for the item of the routing).
/// </summary>
public static class InspectionPlanPointer
{
    public static async Task CheckAsync(IInspectionPlanRepository plans, Guid? planId, Guid plantId, Guid itemId, InspectionType type, CancellationToken ct)
    {
        if (planId is not { } id) return;
        var plan = await plans.GetAsync(id, ct) ?? throw MfgRules.Invalid("The inspection plan does not exist.");
        if (plan.PlantId != plantId) throw MfgRules.Invalid("The inspection plan belongs to another plant.");
        if (plan.Type != type) throw MfgRules.Invalid(type == InspectionType.Fqc ? "A final inspection needs an FQC plan." : "An in-process inspection needs an IPQC plan.");
        if (plan.ItemId != itemId) throw MfgRules.Invalid("The inspection plan is for another item.");
    }
}
