using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;

namespace ERP.MFG.Domain.Engineering;

/// <summary>BR-RT-04: how long one operation takes.</summary>
public static class OperationTimeCalculator
{
    /// <summary><c>Std = Setup + Q x Run</c> (per unit) or <c>Setup + ceil(Q / Batch) x Run</c> (per batch), in minutes. Example: setup 30, run 6, Q 100 = 630.</summary>
    public static decimal StandardMinutes(decimal setup, decimal run, RunTimeBasis basis, decimal? batchSize, decimal quantity)
    {
        MfgRules.ThrowIf(quantity <= 0, MfgErrors.Po001);
        var runPart = basis == RunTimeBasis.PerBatch
            ? Math.Ceiling(quantity / (batchSize is > 0 ? batchSize.Value : throw MfgRules.Fail(MfgErrors.Rt002))) * run
            : quantity * run;
        return setup + runPart;
    }

    public static decimal StandardMinutes(RoutingOperation op, decimal quantity) => StandardMinutes(op.SetupTimeMinutes, op.RunTimeMinutes, op.RunTimeBasis, op.BatchSize, quantity);

    /// <summary>The standard minutes stretched by the work center's efficiency, rounded UP to a whole minute: <c>630 / 0.80 = 787.5 -> 788</c>.</summary>
    public static int CalendarMinutes(decimal standardMinutes, decimal efficiencyPct)
    {
        MfgRules.ThrowIf(efficiencyPct <= 0, MfgErrors.Wc002);
        return (int)Math.Ceiling(standardMinutes / (efficiencyPct / 100m));
    }
}

/// <summary>An operation prepared for scheduling: its times, the efficiency of its work center and the calendar that center works to.</summary>
public sealed record ScheduledOperationInput(
    int OperationSeq,
    string Name,
    string WorkCenterCode,
    decimal StandardMinutes,
    decimal EfficiencyPct,
    int QueueMinutes,
    int MoveMinutes,
    Guid CalendarId,
    WorkingTimeCalculator Calendar);

public sealed record ScheduledOperation(
    int OperationSeq,
    string Name,
    string WorkCenterCode,
    decimal StandardMinutes,
    int CalendarMinutes,
    int QueueMinutes,
    int MoveMinutes,
    WorkPoint Start,
    WorkPoint End,
    DateTime StartTime,
    DateTime EndTime);

public sealed record RoutingLeadTime(IReadOnlyList<ScheduledOperation> Operations, int TotalMinutes, WorkPoint Start, DateOnly StartDate, DateTime StartTime);

/// <summary>BR-RT-05: the lead time of a routing over the calendars of its work centers.</summary>
public static class RoutingLeadTimeCalculator
{
    /// <summary>
    /// Schedules backwards from <paramref name="end"/>: the last operation first. For each operation, on its own calendar, the move time, then the
    /// calendar minutes, then the queue time are taken off the working time (queue and move count as working minutes of that calendar). Total =
    /// sum of queue + calendar minutes + move over all operations. When the next operation uses another calendar the point is handed over by
    /// clock time (the latest working moment at or before it).
    /// </summary>
    public static RoutingLeadTime Backward(IReadOnlyList<ScheduledOperationInput> operations, WorkPoint end, WorkingTimeCalculator endCalendar)
    {
        MfgRules.ThrowIf(operations.Count == 0, MfgErrors.Rt001, ("seq", 0));
        var placed = new List<ScheduledOperation>();
        var point = end;
        Guid? pointCalendar = null; // null = the calendar of the end point (the first operation processed)
        var total = 0;

        foreach (var op in operations.OrderByDescending(o => o.OperationSeq))
        {
            var calendarMinutes = OperationTimeCalculator.CalendarMinutes(op.StandardMinutes, op.EfficiencyPct);
            var calendar = op.Calendar;

            // hand the point over to this operation's calendar
            if (pointCalendar is { } previous && previous != op.CalendarId)
                point = calendar.FromDateTime(placed[^1].StartTime);
            else if (pointCalendar is null && !ReferenceEquals(endCalendar, calendar))
                point = calendar.FromDateTime(endCalendar.ToDateTime(end));

            var opEnd = point;
            var afterMove = calendar.SubtractWorkingMinutes(opEnd, op.MoveMinutes);
            var workStart = calendar.SubtractWorkingMinutes(afterMove, calendarMinutes);
            var opStart = calendar.SubtractWorkingMinutes(workStart, op.QueueMinutes);

            placed.Add(new ScheduledOperation(op.OperationSeq, op.Name, op.WorkCenterCode, op.StandardMinutes, calendarMinutes, op.QueueMinutes, op.MoveMinutes,
                opStart, opEnd, calendar.ToDateTime(opStart), calendar.ToDateTime(opEnd)));
            total += op.QueueMinutes + calendarMinutes + op.MoveMinutes;
            point = opStart;
            pointCalendar = op.CalendarId;
        }

        placed.Reverse();
        var first = placed[0];
        return new RoutingLeadTime(placed, total, first.Start, first.Start.Date, first.StartTime);
    }
}

/// <summary>Forward scheduling (BR-PO-06, an order that cannot be placed backwards): the same per-operation arithmetic as <see cref="RoutingLeadTimeCalculator.Backward"/> run the other way.</summary>
public static class RoutingForwardScheduler
{
    /// <summary>
    /// Places the operations one after the other starting at the earliest working moment at or after <paramref name="notBefore"/> (a clock time of the first
    /// operation's calendar). For each operation, on its own calendar: queue, then the calendar minutes, then the move time (all working minutes); the next
    /// operation starts at the earliest working moment of its calendar at or after the clock time the previous one ended.
    /// </summary>
    public static RoutingLeadTime Forward(IReadOnlyList<ScheduledOperationInput> operations, DateTime notBefore)
    {
        MfgRules.ThrowIf(operations.Count == 0, MfgErrors.Rt001, ("seq", 0));
        var placed = new List<ScheduledOperation>();
        var time = notBefore;
        var total = 0;

        foreach (var op in operations.OrderBy(o => o.OperationSeq))
        {
            var calendarMinutes = OperationTimeCalculator.CalendarMinutes(op.StandardMinutes, op.EfficiencyPct);
            var calendar = op.Calendar;
            var start = calendar.FromDateTimeForward(time);
            var workStart = calendar.AddWorkingMinutes(start, op.QueueMinutes);
            var workEnd = calendar.AddWorkingMinutes(workStart, calendarMinutes);
            var end = calendar.AddWorkingMinutes(workEnd, op.MoveMinutes);

            placed.Add(new ScheduledOperation(op.OperationSeq, op.Name, op.WorkCenterCode, op.StandardMinutes, calendarMinutes, op.QueueMinutes, op.MoveMinutes,
                start, end, calendar.ToDateTime(start), calendar.ToDateTime(end)));
            total += op.QueueMinutes + calendarMinutes + op.MoveMinutes;
            time = calendar.ToDateTime(end);
        }

        var first = placed[0];
        return new RoutingLeadTime(placed, total, first.Start, first.Start.Date, first.StartTime);
    }
}

/// <summary>BR-RT-08 (T2.7): does every operation named by a BOM line exist in the routing that applies?</summary>
public static class BomRoutingCheck
{
    /// <summary>One warning per distinct operation sequence of the BOM lines that the routing does not have. No routing = nothing to check (BR-RT-07).</summary>
    public static IReadOnlyList<string> Warnings(IEnumerable<BomLine> lines, IReadOnlyCollection<int>? routingOperationSeqs, int routingVersion = 0)
    {
        if (routingOperationSeqs is null || routingOperationSeqs.Count == 0) return [];
        return lines.Where(l => l.OperationSeq is { } seq && !routingOperationSeqs.Contains(seq))
            .GroupBy(l => l.OperationSeq!.Value).OrderBy(g => g.Key)
            .Select(g => $"Operation {g.Key} (BOM line {string.Join(", ", g.Select(l => l.LineNo))}: {string.Join(", ", g.Select(l => l.ComponentCode))}) is not in routing version {routingVersion}.")
            .ToList();
    }

    /// <summary>
    /// For an order being created: the operation a material line is attached to. A line whose operation is missing goes to the first operation of the
    /// routing, with a warning; a line without an operation, or an order without a routing, stays unattached.
    /// </summary>
    public static (int? OperationSeq, string? Warning) AssignOperation(int? lineOperationSeq, IReadOnlyCollection<int>? routingOperationSeqs)
    {
        if (lineOperationSeq is null || routingOperationSeqs is null || routingOperationSeqs.Count == 0) return (null, null);
        if (routingOperationSeqs.Contains(lineOperationSeq.Value)) return (lineOperationSeq, null);
        var first = routingOperationSeqs.Min();
        return (first, $"Operation {lineOperationSeq} is not in the routing; the material was attached to operation {first}.");
    }
}
