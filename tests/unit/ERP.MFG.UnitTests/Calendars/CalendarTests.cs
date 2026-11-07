using ERP.MFG.Application.Features.Calendars;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;
using ERP.MFG.Infrastructure.Services;
using ERP.MFG.UnitTests.Common;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Authorization;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Calendars;

public class CalendarTests
{
    private static readonly Guid Plant = Guid.NewGuid();
    private static Shift Make(string code, int sh, int sm, int eh, int em, int brk) => Shift.Create(Plant, code, code, new TimeOnly(sh, sm), new TimeOnly(eh, em), brk);

    // ── Shifts: BR-CAL-01 ────────────────────────────────────────────────────────────────────────────────

    [Fact] public void TC_CAL_001_Normal_Shift_Is_450_Minutes() => Make("CA1", 6, 0, 14, 0, 30).DurationMinutes.Should().Be(450);

    [Fact] public void TC_CAL_002_Overnight_Shift_Is_450_Minutes() => Make("CA3", 22, 0, 6, 0, 30).DurationMinutes.Should().Be(450);

    [Fact] public void TC_CAL_003_A_24_Hour_Shift_Is_Valid() => Make("ALL", 6, 0, 6, 0, 0).DurationMinutes.Should().Be(1440);

    [Theory]
    [InlineData(480)]   // break equals the raw duration
    [InlineData(600)]
    [InlineData(-1)]
    public void TC_CAL_004_A_Break_Not_Shorter_Than_The_Shift_Is_Refused(int breakMinutes)
    {
        var act = () => Make("BAD", 6, 0, 14, 0, breakMinutes);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_CAL_001");
    }

    // ── Overlap: BR-CAL-02 ───────────────────────────────────────────────────────────────────────────────

    private static WorkCalendar Calendar() => WorkCalendar.Create(Plant, "CAL", "Calendar");

    [Fact]
    public void TC_CAL_005_Overlapping_Shifts_On_One_Day_Are_Refused()
    {
        var act = () => Calendar().SetDayPattern(DayOfWeek.Monday, [Make("CA1", 6, 0, 14, 0, 30).ToInfo(), Make("MID", 13, 0, 15, 0, 0).ToInfo()]);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_CAL_002");
    }

    [Fact]
    public void TC_CAL_006_A_Shift_Overlapping_An_Overnight_Shift_Is_Refused()
    {
        var act = () => Calendar().SetDayPattern(DayOfWeek.Monday, [Make("CA2", 14, 0, 22, 0, 30).ToInfo(), Make("NIGHT", 21, 0, 5, 0, 0).ToInfo()]);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_CAL_002");
    }

    [Fact]
    public void TC_CAL_007_Shifts_That_Touch_Are_Fine_Including_The_Overnight_One_Next_To_The_Morning_Shift()
    {
        var ds = new Ds01();
        var act = () => Calendar().SetDayPattern(DayOfWeek.Monday, [ds.Ca1.ToInfo(), ds.Ca2.ToInfo(), ds.Ca3.ToInfo()]);

        act.Should().NotThrow();
    }

    [Fact]
    public void The_Same_Shift_Twice_On_One_Day_Is_Refused()
    {
        var ca1 = Make("CA1", 6, 0, 14, 0, 30).ToInfo();

        ((Action)(() => Calendar().SetDayPattern(DayOfWeek.Monday, [ca1, ca1]))).Should().Throw<BusinessRuleException>();
    }

    // ── Minutes of a day: BR-CAL-03 ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-11-09", 900)]   // Monday
    [InlineData("2026-11-14", 450)]   // Saturday
    [InlineData("2026-11-15", 0)]     // Sunday
    [InlineData("2027-01-01", 0)]     // holiday on a Friday
    public void TC_CAL_008_Working_Minutes_Of_A_Date(string date, int expected)
        => new Ds01().Calculator.GetWorkingMinutes(DateOnly.Parse(date)).Should().Be(expected);

    [Fact]
    public void An_Extra_Working_Day_Uses_The_Shifts_Of_The_Exception()
    {
        var ds = new Ds01();
        ds.Cal5D.AddException(new DateOnly(2026, 11, 15), CalendarExceptionType.ExtraWorkingDay, "Overtime", [ds.Ca3.ToInfo()]); // a Sunday
        ds.Cal5D.AddException(new DateOnly(2026, 11, 10), CalendarExceptionType.ModifiedShifts, "Short day", [ds.Ca1.ToInfo()]);

        var calc = ds.Calculator;
        calc.GetWorkingMinutes(new DateOnly(2026, 11, 15)).Should().Be(450);
        calc.GetWorkingMinutes(new DateOnly(2026, 11, 10)).Should().Be(450);
    }

    [Fact]
    public void TC_CAL_009_Two_Exceptions_On_One_Date_Are_Refused()
    {
        var ds = new Ds01();

        var act = () => ds.Cal5D.AddException(new DateOnly(2027, 1, 1), CalendarExceptionType.Holiday, "again", []);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_GEN_006");
    }

    [Fact]
    public void A_Holiday_With_Shifts_Or_A_Working_Exception_Without_Shifts_Is_Refused()
    {
        var ds = new Ds01();

        ((Action)(() => ds.Cal5D.AddException(new DateOnly(2026, 12, 25), CalendarExceptionType.Holiday, null, [ds.Ca1.ToInfo()]))).Should().Throw<BusinessRuleException>();
        ((Action)(() => ds.Cal5D.AddException(new DateOnly(2026, 12, 26), CalendarExceptionType.ExtraWorkingDay, null, []))).Should().Throw<BusinessRuleException>();
    }

    // ── The calculator: BR-CAL-05 ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TC_CAL_010_Subtracting_1000_Minutes_From_The_End_Of_Monday_Lands_On_Saturday_Minute_350()
    {
        var result = new Ds01().Calculator.SubtractWorkingMinutes(new WorkPoint(new DateOnly(2026, 11, 16), 900), 1000);

        result.Should().Be(new WorkPoint(new DateOnly(2026, 11, 14), 350));
    }

    [Fact]
    public void Adding_Is_The_Mirror_Of_Subtracting()
    {
        var calc = new Ds01().Calculator;

        calc.AddWorkingMinutes(new WorkPoint(new DateOnly(2026, 11, 14), 350), 1000).Should().Be(new WorkPoint(new DateOnly(2026, 11, 16), 900));
        calc.AddWorkingMinutes(new WorkPoint(new DateOnly(2026, 11, 13), 900), 1).Should().Be(new WorkPoint(new DateOnly(2026, 11, 14), 1), "the end of Friday's work, plus one minute, is Saturday's first");
    }

    [Fact]
    public void Zero_Minutes_Stay_Where_They_Are()
    {
        var calc = new Ds01().Calculator;
        var point = new WorkPoint(new DateOnly(2026, 11, 11), 200);

        calc.AddWorkingMinutes(point, 0).Should().Be(point);
        calc.SubtractWorkingMinutes(point, 0).Should().Be(point);
    }

    [Fact]
    public void TC_CAL_011_Adding_Working_Days_Counts_Saturday_But_Not_Sunday()
    {
        var calc = new Ds01().Calculator;

        calc.AddWorkingDays(new DateOnly(2026, 11, 13), 1).Should().Be(new DateOnly(2026, 11, 14));
        calc.AddWorkingDays(new DateOnly(2026, 11, 14), 1).Should().Be(new DateOnly(2026, 11, 16));
    }

    [Fact]
    public void TC_CAL_012_Subtracting_A_Working_Day_Skips_The_Holiday_And_Sunday()
    {
        // 2027-01-04 is a Monday: Sunday 3 is off, so the previous working day is Saturday 2 (the holiday 1 Jan is before that).
        new Ds01().Calculator.SubtractWorkingDays(new DateOnly(2027, 1, 4), 1).Should().Be(new DateOnly(2027, 1, 2));
        new Ds01().Calculator.SubtractWorkingDays(new DateOnly(2027, 1, 2), 1).Should().Be(new DateOnly(2026, 12, 31), "1 January is a holiday");
    }

    [Fact]
    public void Clock_Time_Follows_The_Shifts_And_An_Overnight_Shift_Ends_The_Next_Morning()
    {
        var ds = new Ds01();
        var calc = ds.Calculator;
        calc.ToDateTime(new WorkPoint(new DateOnly(2026, 11, 14), 350)).Should().Be(new DateTime(2026, 11, 14, 11, 50, 0), "Saturday starts at 06:00, 350 minutes later");
        calc.ToDateTime(new WorkPoint(new DateOnly(2026, 11, 9), 450)).Should().Be(new DateTime(2026, 11, 9, 13, 30, 0), "the break is at the end of the shift, so work ends 13:30");
        calc.ToDateTime(new WorkPoint(new DateOnly(2026, 11, 9), 451)).Should().Be(new DateTime(2026, 11, 9, 14, 1, 0));

        var night = WorkCalendar.Create(ds.PlantId, "NIGHT", "Night");
        night.SetDayPattern(DayOfWeek.Monday, [ds.Ca3.ToInfo()]);
        var intervals = new WorkingTimeCalculator(night.ToSnapshot(ds.Shifts)).GetWorkingIntervals(new DateOnly(2026, 11, 9));
        intervals.Should().ContainSingle().Which.Should().Be((new DateTime(2026, 11, 9, 22, 0, 0), new DateTime(2026, 11, 10, 5, 30, 0)));
    }

    [Fact]
    public void A_Calendar_Without_Working_Time_Gives_A_Refusal_Not_An_Endless_Loop()
    {
        var ds = new Ds01();
        var empty = new WorkingTimeCalculator(WorkCalendar.Create(ds.PlantId, "EMPTY", "Empty").ToSnapshot(ds.Shifts));

        ((Action)(() => empty.AddWorkingDays(new DateOnly(2026, 11, 9), 1))).Should().Throw<BusinessRuleException>();
        ((Action)(() => empty.SubtractWorkingMinutes(new WorkPoint(new DateOnly(2026, 11, 9), 0), 10))).Should().Throw<BusinessRuleException>();
        ((Action)(() => empty.AddWorkingMinutes(new WorkPoint(new DateOnly(2026, 11, 9), 0), 10))).Should().Throw<BusinessRuleException>();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────────────────────────────────

    private static (Fakes F, Ds01 D, PlantAccessService Access) Arrange()
    {
        var f = new Fakes();
        var plant = f.AddPlant();
        var d = new Ds01(plant.Id);
        f.Of<Shift>().AddRange([d.Ca1, d.Ca2, d.Ca3]);
        f.Calendars.Add(d.Cal5D);
        f.WorkingTime.Build = _ => d.Calculator;
        var access = new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage));
        return (f, d, access);
    }

    [Fact]
    public async Task Changing_A_Shift_So_That_It_Overlaps_Another_Shift_Of_A_Calendar_Is_Refused()
    {
        var (f, d, access) = Arrange();
        var handler = new UpdateShiftHandler(access, f.Catalog, f.CalendarRepo, f.WorkingTime, f.Uow);

        // CA1 (06-14) stretched to 06-15 would run into CA2 (14-22) on every weekday of CAL-5D.
        var act = () => handler.Handle(new UpdateShiftCommand(d.Ca1.Id, "Ca 1", new TimeOnly(6, 0), new TimeOnly(15, 0), 30), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_CAL_002");
        f.Saves.Should().Be(0);
    }

    [Fact]
    public async Task A_Shift_Change_That_Fits_Is_Saved_And_The_Calendar_Cache_Is_Dropped()
    {
        var (f, d, access) = Arrange();
        var handler = new UpdateShiftHandler(access, f.Catalog, f.CalendarRepo, f.WorkingTime, f.Uow);

        var dto = await handler.Handle(new UpdateShiftCommand(d.Ca1.Id, "Ca 1", new TimeOnly(6, 0), new TimeOnly(14, 0), 60), default);

        dto.DurationMinutes.Should().Be(420);
        f.WorkingTime.Invalidated.Should().Contain(d.Cal5D.Id);
    }

    [Fact]
    public async Task A_Pattern_Day_Needs_Existing_Active_Shifts_Of_The_Same_Plant()
    {
        var (f, d, access) = Arrange();
        var handler = new SetCalendarDayHandler(access, f.CalendarRepo, f.Catalog, f.WorkingTime, f.Uow);
        d.Ca2.SetActive(false);

        await ((Func<Task>)(() => handler.Handle(new SetCalendarDayCommand(d.Cal5D.Id, DayOfWeek.Sunday, [Guid.NewGuid()]), default))).Should().ThrowAsync<BusinessRuleException>("unknown shift");
        await ((Func<Task>)(() => handler.Handle(new SetCalendarDayCommand(d.Cal5D.Id, DayOfWeek.Sunday, [d.Ca2.Id]), default))).Should().ThrowAsync<BusinessRuleException>("inactive shift");

        var ok = await handler.Handle(new SetCalendarDayCommand(d.Cal5D.Id, DayOfWeek.Sunday, [d.Ca1.Id]), default);
        ok.Week.Single(day => day.DayOfWeek == DayOfWeek.Sunday).WorkingMinutes.Should().Be(450);
        f.WorkingTime.Invalidated.Should().Contain(d.Cal5D.Id);
    }

    [Fact]
    public async Task The_Month_View_Shows_The_Minutes_Of_Every_Day_And_Marks_Exceptions()
    {
        var (f, d, access) = Arrange();
        var handler = new GetCalendarMonthHandler(access, f.CalendarRepo, f.WorkingTime);

        var january = await handler.Handle(new GetCalendarMonthQuery(d.Cal5D.Id, 2027, 1), default);

        january.Should().HaveCount(31);
        january.Single(x => x.Date == new DateOnly(2027, 1, 1)).Should().Match<MonthDayDto>(x => x.WorkingMinutes == 0 && x.IsException && x.ExceptionType == CalendarExceptionType.Holiday);
        january.Single(x => x.Date == new DateOnly(2027, 1, 4)).WorkingMinutes.Should().Be(900);
        january.Single(x => x.Date == new DateOnly(2027, 1, 3)).WorkingMinutes.Should().Be(0);
    }

    [Fact]
    public async Task The_Working_Time_Query_Answers_With_Clock_Time()
    {
        var (f, d, access) = Arrange();
        var handler = new WorkingTimeHandler(access, f.CalendarRepo, f.WorkingTime);

        var minutes = await handler.Handle(new WorkingTimeQuery(d.Cal5D.Id, WorkingTimeOperation.SubtractMinutes, new DateOnly(2026, 11, 16), 900, 1000), default);
        var days = await handler.Handle(new WorkingTimeQuery(d.Cal5D.Id, WorkingTimeOperation.AddDays, new DateOnly(2026, 11, 14), 0, 1), default);

        minutes.Should().Be(new WorkingTimeResult(new DateOnly(2026, 11, 14), 350, new DateTime(2026, 11, 14, 11, 50, 0)));
        days.Date.Should().Be(new DateOnly(2026, 11, 16));
    }

    [Fact]
    public async Task TC_GEN_003_A_Shift_Or_Calendar_That_Is_Used_Cannot_Be_Deleted_But_An_Unused_One_Can()
    {
        var (f, d, access) = Arrange();
        var spare = Shift.Create(d.PlantId, "SPARE", "Spare", new TimeOnly(8, 0), new TimeOnly(9, 0), 0);
        f.Of<Shift>().Add(spare);

        var usedShift = () => new DeleteShiftHandler(access, f.Catalog, f.References, f.Uow).Handle(new DeleteShiftCommand(d.Ca1.Id), default);
        (await usedShift.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_GEN_005");

        await new DeleteShiftHandler(access, f.Catalog, f.References, f.Uow).Handle(new DeleteShiftCommand(spare.Id), default);
        spare.IsDeleted.Should().BeTrue();
        (await new GetShiftsHandler(access, f.Catalog).Handle(new GetShiftsQuery(d.PlantId), default)).Select(s => s.Code).Should().NotContain("SPARE");
    }
}

