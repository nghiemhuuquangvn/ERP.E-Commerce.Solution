using ERP.MFG.Domain.Scheduling;

namespace ERP.MFG.Domain.Engineering;

/// <summary>An order's schedule in the plant's local clock time (the application converts it to UTC for storage).</summary>
public sealed record LocalOrderSchedule(DateTime Start, DateTime End, bool IsLate, IReadOnlyDictionary<int, (DateTime Start, DateTime End)> Operations);

/// <summary>
/// BR-PO-06, infinite capacity. With a routing: backward from the end of the due date's working time over the work centers' calendars; when the start would lie
/// before now the order is placed forward from now instead and flagged late. Without a routing: the start is the item's fixed lead time in working days
/// before the due date on the plant calendar; a start before today moves to today and the order is flagged late. Pure: no I/O.
/// </summary>
public static class OrderScheduleCalculator
{
    public static LocalOrderSchedule Calculate(
        IReadOnlyList<ScheduledOperationInput> operations, DateOnly dueDate, DateTime nowLocal, int fixedLeadTimeDays, WorkingTimeCalculator plantCalendar)
    {
        return operations.Count > 0
            ? WithRouting(operations, dueDate, nowLocal)
            : WithoutRouting(dueDate, nowLocal, fixedLeadTimeDays, plantCalendar);
    }

    private static LocalOrderSchedule WithRouting(IReadOnlyList<ScheduledOperationInput> operations, DateOnly dueDate, DateTime nowLocal)
    {
        var last = operations.OrderByDescending(o => o.OperationSeq).First();
        var end = new WorkPoint(dueDate, last.Calendar.GetWorkingMinutes(dueDate)); // the end of that day's work
        var backward = RoutingLeadTimeCalculator.Backward(operations, end, last.Calendar);

        if (backward.StartTime >= nowLocal) return From(backward, isLate: false);
        return From(RoutingForwardScheduler.Forward(operations, nowLocal), isLate: true);
    }

    private static LocalOrderSchedule From(RoutingLeadTime plan, bool isLate)
        => new(plan.StartTime, plan.Operations[^1].EndTime, isLate, plan.Operations.ToDictionary(o => o.OperationSeq, o => (o.StartTime, o.EndTime)));

    private static LocalOrderSchedule WithoutRouting(DateOnly dueDate, DateTime nowLocal, int leadDays, WorkingTimeCalculator calendar)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        var startDay = calendar.SubtractWorkingDays(dueDate, leadDays);
        var late = startDay < today;
        if (late)
        {
            startDay = today;
            dueDate = calendar.AddWorkingDays(today, leadDays); // placed forward: it finishes when the lead time has passed
        }

        return new LocalOrderSchedule(StartOfWork(calendar, startDay), EndOfWork(calendar, dueDate), late || dueDate < DateOnly.FromDateTime(nowLocal), new Dictionary<int, (DateTime, DateTime)>());
    }

    private static DateTime StartOfWork(WorkingTimeCalculator calendar, DateOnly day)
        => calendar.GetWorkingIntervals(day) is { Count: > 0 } intervals ? intervals[0].Start : day.ToDateTime(TimeOnly.MinValue);

    private static DateTime EndOfWork(WorkingTimeCalculator calendar, DateOnly day)
        => calendar.ToDateTime(new WorkPoint(day, calendar.GetWorkingMinutes(day)));
}
