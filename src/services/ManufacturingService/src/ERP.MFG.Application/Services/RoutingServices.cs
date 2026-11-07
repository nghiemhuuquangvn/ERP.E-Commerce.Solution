using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.Application.Services;

public sealed class RoutingResolver(IRoutingRepository routings) : IRoutingResolver
{
    public async Task<Routing?> GetEffectiveRoutingAsync(Guid itemId, Guid plantId, DateOnly date, CancellationToken ct = default)
        => DocumentVersioning.Resolve((await routings.ListApprovedAsync(plantId, ct)).Where(r => r.ItemId == itemId), date);
}

/// <summary>Loads the work centers and calendars of a routing and runs the pure lead-time calculator over them.</summary>
public sealed class OperationTimeService(IPlantCatalog catalog, IWorkingTimeService workingTime) : IOperationTimeService
{
    public async Task<RoutingLeadTime> LeadTimeAsync(Routing routing, decimal quantity, DateOnly endDate, CancellationToken ct = default)
    {
        var operations = routing.Operations.ToList();
        if (operations.Count == 0) throw MfgRules.Invalid("The routing has no operations.");

        var centers = (await catalog.GetManyAsync<WorkCenter>(operations.Select(o => o.WorkCenterId).Distinct().ToList(), ct)).ToDictionary(c => c.Id);
        var calendars = new Dictionary<Guid, WorkingTimeCalculator>();
        var inputs = new List<ScheduledOperationInput>();
        foreach (var op in operations)
        {
            if (!centers.TryGetValue(op.WorkCenterId, out var center)) throw MfgRules.Fail(MfgErrors.Rt003);
            if (!calendars.TryGetValue(center.CalendarId, out var calendar))
                calendars[center.CalendarId] = calendar = await workingTime.ForCalendarAsync(center.CalendarId, ct);
            inputs.Add(new ScheduledOperationInput(op.OperationSeq, op.Name, center.Code, OperationTimeCalculator.StandardMinutes(op, quantity), center.EfficiencyPct,
                op.QueueTimeMinutes, op.MoveTimeMinutes, center.CalendarId, calendar));
        }

        var last = inputs.OrderByDescending(i => i.OperationSeq).First();
        var end = new WorkPoint(endDate, last.Calendar.GetWorkingMinutes(endDate)); // the end of that day's work
        return RoutingLeadTimeCalculator.Backward(inputs, end, last.Calendar);
    }
}
