using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Scheduling;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>BR-PO-06 on the DS-01 calendar: backward from the due date, forward when late, no routing, calendar hand-over (TC-SCH-001..005).</summary>
public class OrderSchedulerTests
{
    private static async Task<OrderCtx> SeededAsync()
    {
        var ctx = new OrderCtx();
        await ctx.SeedAllAsync();
        return ctx;
    }

    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public async Task TC_SCH_001_MO_A_Due_On_Friday_The_13th_Starts_On_Wednesday_The_11th()
    {
        var ctx = await SeededAsync();

        var order = await ctx.CreateOrderAsync(due: D(11, 13));

        order.IsLate.Should().BeFalse();
        ctx.LocalDate(order.PlannedStart).Should().Be(D(11, 11));
        ctx.LocalDate(order.PlannedEnd).Should().Be(D(11, 13));
        var op10 = order.Operations.Single(o => o.OperationSeq == 10);
        var op20 = order.Operations.Single(o => o.OperationSeq == 20);
        var op30 = order.Operations.Single(o => o.OperationSeq == 30);
        ctx.LocalDate(op10.PlannedStart!.Value).Should().Be(D(11, 11));
        ctx.LocalDate(op20.PlannedStart!.Value).Should().Be(D(11, 12));
        ctx.LocalDate(op30.PlannedStart!.Value).Should().Be(D(11, 13));
        ctx.LocalDate(op30.PlannedEnd!.Value).Should().Be(D(11, 13));
        op10.PlannedEnd.Should().BeOnOrBefore(op20.PlannedStart!.Value);
        op20.PlannedEnd.Should().BeOnOrBefore(op30.PlannedStart!.Value);
    }

    [Fact]
    public async Task TC_SCH_002_An_Order_That_Cannot_Start_In_Time_Is_Placed_Forward_From_Now_And_Is_Late()
    {
        var ctx = await SeededAsync();

        var order = await ctx.CreateOrderAsync(due: D(11, 5));

        order.IsLate.Should().BeTrue();
        ctx.Local(order.PlannedStart).Should().BeOnOrAfter(new DateTime(2026, 11, 4, 8, 0, 0));    // not before now (08:00 plant time)
        ctx.Local(order.PlannedEnd).Date.Should().BeAfter(D(11, 5).ToDateTime(TimeOnly.MinValue));
        order.Operations.Select(o => o.PlannedStart).Should().BeInAscendingOrder();
        order.Operations[0].PlannedStart.Should().Be(order.PlannedStart);
    }

    [Fact]
    public async Task TC_SCH_003_Sunday_Is_Skipped_And_Saturday_Only_Uses_One_Shift()
    {
        var ctx = await SeededAsync();

        var order = await ctx.CreateOrderAsync(due: D(11, 16));   // a Monday

        foreach (var op in order.Operations)
        {
            ctx.LocalDate(op.PlannedStart!.Value).Should().NotBe(D(11, 15));
            ctx.LocalDate(op.PlannedEnd!.Value).Should().NotBe(D(11, 15), "nothing is placed on Sunday");
            foreach (var moment in new[] { ctx.Local(op.PlannedStart!.Value), ctx.Local(op.PlannedEnd!.Value) })
                if (DateOnly.FromDateTime(moment) == D(11, 14))
                    (moment.TimeOfDay >= TimeSpan.FromHours(6) && moment.TimeOfDay <= TimeSpan.FromHours(13.5)).Should().BeTrue("Saturday works one shift: 06:00-13:30 of work");
        }
    }

    [Fact]
    public async Task TC_SCH_004_An_Order_Without_A_Routing_Starts_Its_Lead_Time_In_Working_Days_Before_The_Due_Date()
    {
        var ctx = await SeededAsync();
        ctx.SetLeadTime("BTP-CHAN01", 1);

        var order = await ctx.CreateOrderAsync("BTP-CHAN01", 200, D(11, 16));

        order.Operations.Should().BeEmpty();
        order.RoutingId.Should().BeNull();
        ctx.LocalDate(order.PlannedStart).Should().Be(D(11, 14));     // one working day before Monday: Sunday does not count
        ctx.LocalDate(order.PlannedEnd).Should().Be(D(11, 16));
        order.IsLate.Should().BeFalse();
    }

    [Fact]
    public async Task An_Order_Without_A_Routing_Whose_Lead_Time_Has_Already_Started_Is_Late_And_Starts_Today()
    {
        var ctx = await SeededAsync();
        ctx.SetLeadTime("BTP-CHAN01", 3);

        var order = await ctx.CreateOrderAsync("BTP-CHAN01", 200, D(11, 6));   // Friday: three working days earlier is before today

        order.IsLate.Should().BeTrue();
        ctx.LocalDate(order.PlannedStart).Should().Be(D(11, 4));
    }

    [Fact]
    public async Task TC_SCH_005_Changing_The_Due_Date_Of_A_Draft_Moves_The_Whole_Schedule()
    {
        var ctx = await SeededAsync();
        var order = await ctx.CreateOrderAsync(due: D(11, 13));

        var moved = await ctx.UpdateOrderAsync(order.Id, 100, D(11, 20));

        moved.DueDate.Should().Be(D(11, 20));
        ctx.LocalDate(moved.PlannedStart).Should().Be(D(11, 18));
        ctx.LocalDate(moved.PlannedEnd).Should().Be(D(11, 20));
        moved.Operations.Select(o => ctx.LocalDate(o.PlannedStart!.Value)).Should().Equal(D(11, 18), D(11, 19), D(11, 20));
        moved.Materials.Should().HaveCount(7);   // same lines
    }

    [Fact]
    public async Task A_Bigger_Quantity_Makes_Every_Operation_Longer()
    {
        var ctx = await SeededAsync();
        var small = await ctx.CreateOrderAsync(quantity: 100);
        var big = await ctx.CreateOrderAsync(quantity: 400);

        big.Operations.Sum(o => o.CalendarMinutes).Should().BeGreaterThan(small.Operations.Sum(o => o.CalendarMinutes));
        big.PlannedStart.Should().BeBefore(small.PlannedStart);
    }

    [Fact]
    public async Task The_Painting_Operation_Is_Stretched_By_The_Efficiency_Of_Its_Work_Center()
    {
        var ctx = await SeededAsync();

        var order = await ctx.CreateOrderAsync(quantity: 100);

        var painting = order.Operations.Single(o => o.OperationSeq == 20);
        painting.StandardMinutes.Should().Be(630m);
        painting.CalendarMinutes.Should().Be(788);     // 630 / 0.80
    }

    // ── the forward calculator on its own ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Forward_Scheduling_Starts_At_The_Next_Working_Moment_And_Places_One_Operation_After_The_Other()
    {
        var ctx = await SeededAsync();
        var calendar = ctx.D.Calculator;
        ScheduledOperationInput Op(int seq, int minutes) => new(seq, "op" + seq, "WC", minutes, 100, 0, 0, ctx.D.Cal5D.Id, calendar);

        // Saturday 2026-11-07 13:00 is half an hour before the end of the single Saturday shift (06:00-13:30)
        var plan = RoutingForwardScheduler.Forward([Op(10, 60), Op(20, 30)], new DateTime(2026, 11, 7, 13, 0, 0));

        plan.Operations[0].StartTime.Should().Be(new DateTime(2026, 11, 7, 13, 0, 0));
        plan.Operations[0].EndTime.Should().Be(new DateTime(2026, 11, 9, 6, 30, 0));       // 30 minutes Saturday, then 30 on Monday (Sunday is off)
        plan.Operations[1].StartTime.Should().Be(new DateTime(2026, 11, 9, 6, 30, 0));
        plan.Operations[1].EndTime.Should().Be(new DateTime(2026, 11, 9, 7, 0, 0));
        plan.TotalMinutes.Should().Be(90);
    }

    [Fact]
    public async Task Forward_Scheduling_From_A_Time_Outside_Working_Hours_Waits_For_The_Next_Shift()
    {
        var ctx = await SeededAsync();
        var calendar = ctx.D.Calculator;

        calendar.ToDateTime(calendar.FromDateTimeForward(new DateTime(2026, 11, 8, 10, 0, 0))).Should().Be(new DateTime(2026, 11, 9, 6, 0, 0));   // Sunday morning -> Monday 06:00
        calendar.ToDateTime(calendar.FromDateTimeForward(new DateTime(2026, 11, 4, 22, 0, 0))).Should().Be(new DateTime(2026, 11, 5, 6, 0, 0));   // after the last shift
        calendar.FromDateTimeForward(new DateTime(2026, 11, 4, 3, 0, 0)).Should().Be(new WorkPoint(D(11, 4), 0));                                 // before the first shift
        calendar.FromDateTimeForward(new DateTime(2026, 11, 4, 8, 0, 0)).Should().Be(new WorkPoint(D(11, 4), 120));                               // inside the first shift
    }

    [Fact]
    public async Task Forward_Scheduling_Hands_Over_Between_Work_Centers_With_Different_Calendars()
    {
        var ctx = await SeededAsync();
        var fiveDays = ctx.D.Calculator;
        // a second calendar that works only on Wednesday and Thursday, 06:00-13:30
        var sparse = WorkCalendar.Create(ctx.Plant.Id, "CAL-WT", "Wed Thu");
        sparse.SetDayPattern(DayOfWeek.Wednesday, [ctx.D.Ca1.ToInfo()]);
        sparse.SetDayPattern(DayOfWeek.Thursday, [ctx.D.Ca1.ToInfo()]);
        var sparseCalc = new WorkingTimeCalculator(sparse.ToSnapshot(ctx.D.Shifts));

        var plan = RoutingForwardScheduler.Forward(
        [
            new ScheduledOperationInput(10, "a", "WC1", 450, 100, 0, 0, ctx.D.Cal5D.Id, fiveDays),
            new ScheduledOperationInput(20, "b", "WC2", 60, 100, 0, 0, sparse.Id, sparseCalc),
        ], new DateTime(2026, 11, 2, 6, 0, 0));   // Monday

        plan.Operations[0].EndTime.Should().Be(new DateTime(2026, 11, 2, 13, 30, 0));      // a whole Monday shift
        plan.Operations[1].StartTime.Should().Be(new DateTime(2026, 11, 4, 6, 0, 0));      // the next day the second calendar works
        plan.Operations[1].EndTime.Should().Be(new DateTime(2026, 11, 4, 7, 0, 0));
    }
}
