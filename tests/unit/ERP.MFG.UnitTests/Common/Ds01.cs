using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;

namespace ERP.MFG.UnitTests.Common;

/// <summary>The calendar part of the QA data set DS-01 (test cases §1.2-1.3): three shifts of 450 working minutes, CAL-5D Monday-Friday CA1+CA2, Saturday CA1, Sunday off, 2027-01-01 a holiday.</summary>
public sealed class Ds01
{
    public Guid PlantId { get; }
    public Shift Ca1 { get; }
    public Shift Ca2 { get; }
    public Shift Ca3 { get; }
    public WorkCalendar Cal5D { get; }

    public Ds01(Guid? plantId = null)
    {
        PlantId = plantId ?? Guid.NewGuid();
        Ca1 = Shift.Create(PlantId, "CA1", "Ca 1", new TimeOnly(6, 0), new TimeOnly(14, 0), 30);
        Ca2 = Shift.Create(PlantId, "CA2", "Ca 2", new TimeOnly(14, 0), new TimeOnly(22, 0), 30);
        Ca3 = Shift.Create(PlantId, "CA3", "Ca 3", new TimeOnly(22, 0), new TimeOnly(6, 0), 30);

        Cal5D = WorkCalendar.Create(PlantId, "CAL-5D", "Five-day week");
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            Cal5D.SetDayPattern(day, [Ca1.ToInfo(), Ca2.ToInfo()]);
        Cal5D.SetDayPattern(DayOfWeek.Saturday, [Ca1.ToInfo()]);
        Cal5D.AddException(new DateOnly(2027, 1, 1), CalendarExceptionType.Holiday, "New year", []);
    }

    public IEnumerable<ShiftInfo> Shifts => [Ca1.ToInfo(), Ca2.ToInfo(), Ca3.ToInfo()];

    public WorkingTimeCalculator Calculator => new(Cal5D.ToSnapshot(Shifts));

    public static WorkCenterValues CenterValues(Guid calendarId, WorkCenterType type = WorkCenterType.Machine, int resources = 1, decimal eff = 100, decimal util = 100,
        decimal labor = 0, decimal machine = 0, Guid? vendor = null)
        => new("Work center", null, calendarId, type, resources, eff, util, labor, machine, 0, 0, vendor);
}
