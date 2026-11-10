using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

public class BackflushCalculatorTests
{
    [Theory]
    [InlineData(62, 1600, 100, 0, 0, 992)]       // TC-BF-001: 62 pieces x 16
    [InlineData(100, 1600, 100, 0, 992, 608)]    // TC-BF-002: the second report takes the rest of 1 600
    [InlineData(60, 1600, 100, 0, 0, 960)]       // an exact value is not pushed up by division residue
    [InlineData(1, 52.5, 100, 2, 0, 0.53)]       // TC-BF-004: 0.525 -> 0.53
    [InlineData(2, 52.5, 100, 2, 0.53, 0.52)]    //            1.05  -> 1.05 - 0.53
    [InlineData(3, 52.5, 100, 2, 1.05, 0.53)]    //            1.575 -> 1.58 - 1.05, total 1.58 not 1.59
    [InlineData(1, 52.5, 100, 2, 5, 0)]          // never negative
    [InlineData(0, 52.5, 100, 2, 0, 0)]
    [InlineData(10, 52.5, 0, 2, 0, 0)]
    public void Consumption_is_computed_on_the_cumulative_output(decimal output, decimal required, decimal orderQty, int decimals, decimal consumed, decimal expected)
        => BackflushCalculator.Quantity(output, required, orderQty, decimals, consumed).Should().Be(expected);

    [Fact]
    public void Three_reports_of_one_piece_consume_1_58_in_total()
    {
        decimal consumed = 0;
        for (var cumulative = 1; cumulative <= 3; cumulative++)
            consumed += BackflushCalculator.Quantity(cumulative, 52.5m, 100m, 2, consumed);
        consumed.Should().Be(1.58m);
    }
}

public class OperationInputTests
{
    [Fact]
    public void The_first_operation_may_take_the_quantity_plus_the_over_production_tolerance_and_later_ones_the_good_output_before()
    {
        OperationInput.Available(true, 100, 5, 0).Should().Be(105);
        OperationInput.Available(false, 100, 5, 60).Should().Be(60);
    }

    [Theory]
    [InlineData(true, false, 100, 0, 100, true)]     // TC-OP-014: good 98 + scrap 2
    [InlineData(true, false, 100, 0, 99, false)]
    [InlineData(false, true, 100, 60, 60, true)]     // the previous one is done and everything it gave has been taken
    [InlineData(false, false, 100, 60, 60, false)]   // TC-OP-015: the previous one is not done yet
    [InlineData(false, true, 100, 0, 10, false)]     // nothing to take
    public void An_operation_completes_when_its_predecessor_is_done_and_all_its_input_is_taken(bool first, bool previousCompleted, decimal qty, decimal previousGood, decimal taken, bool done)
        => OperationInput.IsComplete(first, previousCompleted, qty, previousGood, taken).Should().Be(done);
}

public class LaborTimeLogTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc);
    private static TimeLogWorker Worker => new(Guid.NewGuid(), "NV001", "Employee NV001", null, null);
    private static LaborTimeLog Open() => LaborTimeLog.StartNow(Guid.NewGuid(), Guid.NewGuid(), 10, Worker, T0, "u");

    private static void Fails(Action act, string code) => act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(code);

    [Fact] // TC-OP-005
    public void Duration_is_the_elapsed_time_minus_the_pauses()
    {
        var log = Open();
        log.Pause(T0.AddHours(1));
        log.Resume(T0.AddHours(1).AddMinutes(15));
        log.Stop(T0.AddHours(2));

        log.DurationMinutes.Should().Be(105m);
        log.PausedMinutes.Should().Be(15m);
        log.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Stopping_a_paused_log_ends_the_pause_with_it()
    {
        var log = Open();
        log.Pause(T0.AddHours(1));
        log.Stop(T0.AddHours(1).AddMinutes(15));

        (log.DurationMinutes, log.PausedMinutes).Should().Be((60m, 15m));   // 75 minutes elapsed, 15 of them paused
    }

    [Fact]
    public void A_log_that_is_not_running_cannot_be_paused_and_a_stopped_one_cannot_be_stopped()
    {
        var log = Open();
        log.Pause(T0.AddMinutes(10));
        Fails(() => log.Pause(T0.AddMinutes(11)), MfgErrors.Op001);
        log.Resume(T0.AddMinutes(12));
        Fails(() => log.Resume(T0.AddMinutes(13)), MfgErrors.Op001);
        log.Stop(T0.AddMinutes(20));
        Fails(() => log.Stop(T0.AddMinutes(21)), MfgErrors.Op001);
    }

    [Fact] // TC-OP-006
    public void A_manual_entry_needs_an_end_after_the_start_and_nothing_in_the_future()
    {
        var now = T0.AddHours(3);
        Fails(() => LaborTimeLog.Manual(Guid.NewGuid(), Guid.NewGuid(), 10, Worker, T0.AddHours(2), T0.AddHours(1), now, "u"), MfgErrors.Op005);   // End < Start
        Fails(() => LaborTimeLog.Manual(Guid.NewGuid(), Guid.NewGuid(), 10, Worker, now.AddHours(1), now.AddHours(2), now, "u"), MfgErrors.Op005);   // Start in the future

        var ok = LaborTimeLog.Manual(Guid.NewGuid(), Guid.NewGuid(), 10, Worker, T0, T0.AddHours(2), now, "u");
        ok.Should().Match<LaborTimeLog>(l => l.DurationMinutes == 120m && l.IsManual && !l.IsOpen);
    }

    [Fact]
    public void Overlap_treats_an_open_log_as_running_to_the_end_of_time()
    {
        var open = Open();
        open.Overlaps(T0.AddHours(5), null).Should().BeTrue();
        open.Overlaps(T0.AddHours(-2), T0.AddHours(-1)).Should().BeFalse();

        var closed = Open();
        closed.Stop(T0.AddHours(1));
        closed.Overlaps(T0.AddHours(1), T0.AddHours(2)).Should().BeFalse();       // touching ends do not overlap
        closed.Overlaps(T0.AddMinutes(30), T0.AddHours(2)).Should().BeTrue();
    }
}

public class OperationExecutionTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> act) => (await act.Should().ThrowAsync<BusinessRuleException>()).Which;

    // ── Time logs ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-OP-001
    public async Task Start_on_a_ready_operation_runs_it_and_starts_the_order()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();

        var log = await c.StartAsync(order, 10);

        log.State.Should().Be("Running");
        log.EmployeeNumber.Should().Be("NV001");
        order.Status.Should().Be(OrderStatus.InProgress);
        order.ActualStart.Should().NotBeNull();
        c.Op(order, 10).Status.Should().Be(OperationStatus.Running);
        c.Op(order, 10).ActualStart.Should().NotBeNull();
    }

    [Fact] // TC-OP-002
    public async Task Start_on_a_pending_operation_is_refused()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();

        (await Refused(() => c.StartAsync(order, 20))).RuleName.Should().Be(MfgErrors.Op001);
        c.TimeLogs.Items.Should().BeEmpty();
        order.Status.Should().Be(OrderStatus.Released);
    }

    [Fact] // TC-OP-003
    public async Task Several_employees_share_one_operation()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();

        await c.StartAsync(order, 10, c.Emp1);
        await c.StartAsync(order, 10, c.Emp2);

        c.TimeLogs.Items.Count(l => l.IsOpen).Should().Be(2);
        c.Op(order, 10).Status.Should().Be(OperationStatus.Running);
    }

    [Fact] // TC-OP-004
    public async Task One_employee_cannot_have_two_overlapping_sessions()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.StartAsync(order, 10, c.Emp1);

        (await Refused(() => c.StartAsync(order, 10, c.Emp1))).RuleName.Should().Be(MfgErrors.Op006);

        var other = await c.ManualTimeAsync(order, 10, c.Clock.Now.UtcDateTime.AddHours(-5), c.Clock.Now.UtcDateTime.AddHours(-4), c.Emp2);   // another person, any time
        other.IsManual.Should().BeTrue();
        (await Refused(() => c.ManualTimeAsync(order, 10, c.Clock.Now.UtcDateTime.AddMinutes(-270), c.Clock.Now.UtcDateTime.AddHours(-3), c.Emp2))).RuleName.Should().Be(MfgErrors.Op006);   // shares 30 minutes with the entry above
    }

    [Fact]
    public async Task The_operation_follows_its_logs_through_pause_resume_and_stop()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        var a = await c.StartAsync(order, 10, c.Emp1);
        var b = await c.StartAsync(order, 10, c.Emp2);

        await c.PauseLogAsync(a.Id);
        c.Op(order, 10).Status.Should().Be(OperationStatus.Running);          // one still runs
        await c.PauseLogAsync(b.Id);
        c.Op(order, 10).Status.Should().Be(OperationStatus.Paused);
        await c.ResumeLogAsync(a.Id);
        c.Op(order, 10).Status.Should().Be(OperationStatus.Running);
        await c.StopLogAsync(a.Id);
        c.Op(order, 10).Status.Should().Be(OperationStatus.Paused);           // only the paused one is left
        await c.StopLogAsync(b.Id);                                              // stopping a paused log: Paused -> Running -> Ready
        c.Op(order, 10).Status.Should().Be(OperationStatus.Ready);
    }

    [Fact] // TC-OP-005 through the handlers
    public async Task Duration_with_a_pause_is_105_minutes()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        var start = c.Clock.Now;
        var log = await c.StartAsync(order, 10);
        c.Clock.Now = start.AddHours(1);
        await c.PauseLogAsync(log.Id);
        c.Clock.Now = start.AddHours(1).AddMinutes(15);
        await c.ResumeLogAsync(log.Id);
        c.Clock.Now = start.AddHours(2);

        var stopped = await c.StopLogAsync(log.Id);

        (stopped.DurationMinutes, stopped.PausedMinutes, stopped.State).Should().Be((105m, 15m, "Stopped"));
    }

    [Fact] // TC-OP-006
    public async Task A_manual_time_with_bad_dates_is_refused()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        var now = c.Clock.Now.UtcDateTime;

        (await Refused(() => c.ManualTimeAsync(order, 10, now.AddHours(-1), now.AddHours(-2)))).RuleName.Should().Be(MfgErrors.Op005);
        (await Refused(() => c.ManualTimeAsync(order, 10, now.AddHours(1), now.AddHours(2)))).RuleName.Should().Be(MfgErrors.Op005);
    }

    [Fact]
    public async Task A_retired_machine_cannot_be_chosen_and_an_inactive_employee_cannot_start()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        var machine = Machine.Create(c.Plant.Id, "M-ASM-01", "Assembly bench", c.Op(order, 10).WorkCenterId, 30, null);
        c.F.Of<Machine>().Add(machine);

        (await c.StartAsync(order, 10, machine: machine.Id)).MachineId.Should().Be(machine.Id);
        machine.SetStatus(MachineStatus.Retired);
        (await Refused(() => c.StartAsync(order, 10, c.Emp2, machine.Id))).RuleName.Should().Be(MfgErrors.Op010);

        var gone = c.F.AddEmployee("NV009", active: false);
        await Refused(() => c.StartAsync(order, 10, gone));
    }

    [Fact]
    public async Task Hold_pauses_running_work_and_resume_leaves_it_paused()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        var log = await c.StartAsync(order, 10);

        await c.HoldAsync(order.Id, c.HoldReason.Id);

        c.TimeLogs.Items.Single().IsPaused.Should().BeTrue();
        c.Op(order, 10).Status.Should().Be(OperationStatus.Paused);
        (await Refused(() => c.ResumeLogAsync(log.Id))).RuleName.Should().Be(MfgErrors.Po011);   // still on hold
        (await Refused(() => c.StartAsync(order, 10, c.Emp2))).RuleName.Should().Be(MfgErrors.Op001);   // BR-OP-02: an order on hold is "not ready"

        await c.ResumeAsync(order.Id);                                  // the order, not the log
        c.TimeLogs.Items.Single().IsPaused.Should().BeTrue();
        (await c.ResumeLogAsync(log.Id)).State.Should().Be("Running");
        c.Op(order, 10).Status.Should().Be(OperationStatus.Running);
    }

    // ── Reports ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-OP-007
    public async Task A_valid_report_updates_the_operation_and_makes_the_next_one_ready()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();

        var result = await c.ReportAsync(order, 10, good: 60, scrap: 2);

        result.IsPending.Should().BeFalse();
        (result.Report.Status, result.Report.DocNo, result.Report.GoodQty, result.Report.ScrapQty).Should().Be((OperationReportStatus.Posted, "OR-2611-00001", 60m, 2m));
        var op10 = c.Op(order, 10);
        (op10.GoodQty, op10.ScrapQty).Should().Be((60m, 2m));
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready);
        order.ScrappedQty.Should().Be(2m);
        order.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact] // TC-OP-008 / 009 / 010 / 011
    public async Task A_report_needs_a_positive_total_non_negative_parts_and_a_reason_for_scrap_and_rework()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();

        (await Refused(() => c.ReportAsync(order, 10, 0))).RuleName.Should().Be(MfgErrors.Op002);
        (await Refused(() => c.ReportAsync(order, 10, -1))).RuleName.Should().Be(MfgErrors.Op002);
        (await Refused(() => c.ReportAsync(order, 10, 10, scrap: 2, omitReasons: true))).RuleName.Should().Be(MfgErrors.Op003);
        (await Refused(() => c.ReportAsync(order, 10, 10, rework: 1, omitReasons: true))).RuleName.Should().Be(MfgErrors.Op003);
        c.Reports.Items.Should().BeEmpty();
        c.Stock.MovementCalls.Should().Be(0);
    }

    [Fact]
    public async Task A_scrap_reason_must_be_of_type_scrap()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        var act = () => new ERP.MFG.Application.Features.Operations.ReportOperationHandler(c.Access, c.Repo, c.Reports, c.Documents, c.PlantSettings, c.F.ReasonRepo, c.F.UnitClient, c.F.Catalog,
                c.Planner, c.Closer, c.Numbers, c.PlantTime, c.Posting, c.User, c.F.Uow)
            .Handle(new ERP.MFG.Application.Features.Operations.ReportOperationCommand(order.Id, 10, 10, 2, 0, c.ReworkReason.Id, null, null, null, null), default);

        (await Refused(act)).RuleName.Should().Be(MfgErrors.Op003);
    }

    [Fact] // TC-OP-012
    public async Task The_next_operation_cannot_take_more_than_the_good_output_before_it()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 60);

        var ex = await Refused(() => c.ReportAsync(order, 20, 61));

        ex.RuleName.Should().Be(MfgErrors.Op004);
        ex.Message.Should().Contain("60");
        (await c.ReportAsync(order, 20, 60)).Report.Status.Should().Be(OperationReportStatus.Posted);
    }

    [Fact] // TC-OP-013
    public async Task The_first_operation_may_take_the_quantity_plus_the_tolerance_and_not_a_piece_more()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        c.Stock.Stock(c.Items["NVL-VIT01"].Id, c.RawWarehouse, 200);   // the backflush of 105 pieces needs 1 680

        await c.ReportAsync(order, 10, 105);

        (await Refused(() => c.ReportAsync(order, 10, 1))).RuleName.Should().Be(MfgErrors.Op004);
    }

    [Fact] // TC-OP-014
    public async Task The_first_operation_completes_when_good_plus_scrap_reach_the_quantity()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        var log = await c.StartAsync(order, 10);

        await c.ReportAsync(order, 10, 98, scrap: 2);

        var op = c.Op(order, 10);
        op.Status.Should().Be(OperationStatus.Completed);
        op.ActualEnd.Should().NotBeNull();
        c.TimeLogs.Items.Single().IsOpen.Should().BeFalse("completing the operation closes its open time logs");
        log.Id.Should().Be(c.TimeLogs.Items.Single().Id);
    }

    [Fact] // TC-OP-015
    public async Task A_later_operation_does_not_complete_while_the_one_before_is_open()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 60);

        await c.ReportAsync(order, 20, 60);

        c.Op(order, 10).Status.Should().NotBe(OperationStatus.Completed);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready);
    }

    [Fact] // TC-OP-016
    public async Task A_completed_operation_takes_no_more_reports()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 98, scrap: 2);

        (await Refused(() => c.ReportAsync(order, 10, 1))).RuleName.Should().Be(MfgErrors.Op009);
    }

    [Fact]
    public async Task Completing_an_operation_completes_the_next_ones_that_have_taken_all_their_input()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 60);
        await c.ReportAsync(order, 20, 60);                                // taken all 60 but op 10 is not done: stays open

        await c.ReportAsync(order, 10, 40);                                // op 10 done (100) -> op 20 must now take 100

        c.Op(order, 10).Status.Should().Be(OperationStatus.Completed);
        c.Op(order, 20).Status.Should().NotBe(OperationStatus.Completed);
        await c.ReportAsync(order, 20, 40);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Completed);
    }

    [Fact] // TC-OP-017
    public async Task A_supervisor_completes_an_operation_by_hand_and_reopens_it()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 90);

        (await c.CompleteOperationAsync(order, 10)).Status.Should().Be(OperationStatus.Completed);
        (await c.ReopenOperationAsync(order, 10)).Status.Should().Be(OperationStatus.Ready);
        c.Op(order, 10).ActualEnd.Should().BeNull();
    }

    [Fact]
    public async Task An_operation_cannot_be_reopened_once_the_next_one_has_work()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 90);
        await c.CompleteOperationAsync(order, 10);
        await c.ReportAsync(order, 20, 10);

        (await Refused(() => c.ReopenOperationAsync(order, 10))).RuleName.Should().Be(MfgErrors.Op007);
    }

    [Fact]
    public async Task A_pending_operation_cannot_be_completed_and_a_finished_one_cannot_be_completed_twice()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();

        (await Refused(() => c.CompleteOperationAsync(order, 20))).RuleName.Should().Be(MfgErrors.Op001);
        await c.CompleteOperationAsync(order, 10);
        (await Refused(() => c.CompleteOperationAsync(order, 10))).RuleName.Should().Be(MfgErrors.Op009);
    }

    [Fact]
    public async Task Reporting_on_a_pending_operation_or_an_order_on_hold_is_refused() // TC-OP-002, TC-OP-018
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();

        (await Refused(() => c.ReportAsync(order, 20, 5))).RuleName.Should().Be(MfgErrors.Op001);
        await c.HoldAsync(order.Id, c.HoldReason.Id);
        (await Refused(() => c.ReportAsync(order, 10, 5))).RuleName.Should().Be(MfgErrors.Po011);
    }

    [Fact]
    public async Task When_operations_need_not_run_in_sequence_all_of_them_are_ready_after_release()
    {
        var c = new OpsCtx();
        c.PlantSettings.Sequential = false;
        var order = await c.ReleasedAsync();

        order.Operations.Select(o => o.Status).Should().OnlyContain(s => s == OperationStatus.Ready);
    }

    [Fact]
    public async Task The_same_idempotency_key_returns_the_same_report_and_another_body_is_a_conflict()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();

        var first = await c.ReportAsync(order, 10, 60, key: "rep-1");
        var again = await c.ReportAsync(order, 10, 60, key: "rep-1");

        again.Report.Id.Should().Be(first.Report.Id);
        c.Reports.Items.Should().ContainSingle();
        c.Documents.Docs.Where(d => d.Type == StockDocumentType.Backflush).Should().ContainSingle();
        c.Op(order, 10).GoodQty.Should().Be(60m);
        (await Refused(() => c.ReportAsync(order, 10, 61, key: "rep-1"))).RuleName.Should().Be(MfgErrors.Gen007);
    }

    // ── Backflush ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-BF-001
    public async Task A_report_on_operation_10_consumes_992_screws_from_the_warehouse()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        var vit = c.Items["NVL-VIT01"].Id;

        var result = await c.ReportAsync(order, 10, 60, scrap: 2);

        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(992m);
        c.Mat(order, "NVL-VIT01").IssuedQty.Should().Be(0m, "backflush is consumption, not an issue");
        c.Stock.Total(vit).Should().Be(1600m - 992m);
        result.Backflush!.Should().Match<StockDocumentDto>(d => d.Type == StockDocumentType.Backflush && d.Status == StockDocumentStatus.Posted && d.DocNo == "BF-2611-00001");
        result.Backflush.Lines.Should().ContainSingle().Which.Should().Match<StockDocumentLineDto>(l => l.Qty == 992m && l.IsFromReservation && l.MovementKey == "BF-2611-00001#1");
        c.Stock.HeldFor(order.Id, vit).Should().Be(608m, "the reservation shrank by what was consumed");
    }

    [Fact] // TC-BF-002
    public async Task The_second_report_takes_the_rest_so_that_the_total_is_1600()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 60, scrap: 2);

        await c.ReportAsync(order, 10, 38);

        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(1600m);
        c.BackflushMovements("NVL-VIT01").Select(m => m.Quantity).Should().Equal(992m, 608m);
        c.Stock.Total(c.Items["NVL-VIT01"].Id).Should().Be(0m);
    }

    [Fact] // TC-BF-003
    public async Task The_phantom_kit_is_consumed_on_operation_30()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 100);
        await c.ReportAsync(order, 20, 100);

        var result = await c.ReportAsync(order, 30, 50);

        c.Mat(order, "NVL-THUNG01").ConsumedQty.Should().Be(50m);
        c.Mat(order, "NVL-XOP01").ConsumedQty.Should().Be(100m);
        result.Backflush!.Lines.Should().HaveCount(2);
        c.Op(order, 30).GoodQty.Should().Be(50m);
    }

    [Fact] // TC-BF-004
    public async Task Rounding_never_accumulates_over_three_reports()
    {
        var c = new OpsCtx { SonBackflush = true };
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 3);

        foreach (var _ in Enumerable.Range(0, 3)) await c.ReportAsync(order, 20, 1);

        c.BackflushMovements("NVL-SON01").Select(m => m.Quantity).Should().Equal(0.53m, 0.52m, 0.53m);
        c.Mat(order, "NVL-SON01").ConsumedQty.Should().Be(1.58m);
    }

    [Fact] // TC-BF-005
    public async Task A_shortage_refuses_the_whole_report_and_stores_nothing()
    {
        var c = new OpsCtx();
        var draft = await c.DraftAsync();
        var vit = c.Items["NVL-VIT01"].Id;
        c.Stock.Balances.Remove(vit);
        c.Stock.Stock(vit, c.RawWarehouse, 900);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        var calls = c.Stock.MovementCalls;

        var ex = await Refused(() => c.ReportAsync(order, 10, 60));   // needs 960

        ex.RuleName.Should().Be(MfgErrors.Mat008);
        c.Op(order, 10).GoodQty.Should().Be(0m);
        c.Reports.Items.Should().BeEmpty();
        c.Documents.Docs.Should().BeEmpty();
        c.Stock.MovementCalls.Should().Be(calls);
        order.Status.Should().Be(OrderStatus.Released);
    }

    [Fact] // TC-BF-006
    public async Task When_the_plant_allows_negative_stock_the_shortage_is_booked_below_zero_and_flagged()
    {
        var c = new OpsCtx();
        c.PlantSettings.AllowNegativeStockOnBackflush = true;
        var draft = await c.DraftAsync();
        var vit = c.Items["NVL-VIT01"].Id;
        c.Stock.Balances.Remove(vit);
        c.Stock.Stock(vit, c.RawWarehouse, 900);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);

        var result = await c.ReportAsync(order, 10, 60);

        c.Stock.Total(vit).Should().Be(-60m);
        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(960m);
        result.Backflush!.Lines.Select(l => (l.Qty, l.IsFromReservation)).Should().Equal((900m, true), (60m, false));
        c.Documents.Docs.Single().Lines.Select(l => l.IsNegativeStock).Should().Equal(false, true);
        c.Stock.Requests.Last().AllowNegative.Should().BeTrue();
    }

    [Fact]
    public async Task A_lot_controlled_item_cannot_go_negative()
    {
        var c = new OpsCtx { SonBackflush = true };
        c.PlantSettings.AllowNegativeStockOnBackflush = true;
        var draft = await c.DraftAsync();
        var son = c.Items["NVL-SON01"].Id;
        c.Replace("NVL-SON01", p => p with { IsLotControlled = true });
        c.Stock.Balances.Remove(son);
        c.Stock.Lot(son, c.RawWarehouse, "L-SON-A", 10);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        await c.ReportAsync(order, 10, 100);

        (await Refused(() => c.ReportAsync(order, 20, 100))).RuleName.Should().Be(MfgErrors.Mat008);   // needs 52.5, 10 in stock, no lot to name for the rest
    }

    [Fact] // TC-BF-008
    public async Task Backflush_takes_the_lot_that_expires_first_and_never_an_expired_one()
    {
        var c = new OpsCtx { SonBackflush = true };
        var draft = await c.DraftAsync();
        var son = c.Items["NVL-SON01"].Id;
        c.Replace("NVL-SON01", p => p with { IsLotControlled = true });
        c.Stock.Balances.Remove(son);
        var a = c.Stock.Lot(son, c.RawWarehouse, "L-SON-A", 60, expiry: new DateTime(2027, 5, 1));
        var x = c.Stock.Lot(son, c.RawWarehouse, "L-SON-X", 20, expiry: new DateTime(2026, 10, 1));
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        await c.ReportAsync(order, 10, 100);

        await c.ReportAsync(order, 20, 40);

        var movements = c.BackflushMovements("NVL-SON01").ToList();
        movements.Should().ContainSingle().Which.Should().Match<FakeStock.Booked>(m => m.Quantity == 21.00m && m.LotId == a);
        movements.Should().NotContain(m => m.LotId == x);
    }

    [Fact]
    public async Task A_report_with_nothing_to_backflush_is_applied_at_once_without_calling_the_warehouse()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 100);
        var calls = c.Stock.MovementCalls;

        var result = await c.ReportAsync(order, 20, 40);     // SON is issued by hand: no backflush line on operation 20

        result.Backflush.Should().BeNull();
        result.IsPending.Should().BeFalse();
        c.Stock.MovementCalls.Should().Be(calls);
        c.Op(order, 20).GoodQty.Should().Be(40m);
        c.Documents.Docs.Count(d => d.Type == StockDocumentType.Backflush).Should().Be(1);
    }

    [Fact]
    public async Task A_warehouse_outage_after_the_check_leaves_the_report_pending_and_the_job_applies_it_once()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        c.Stock.Intercept = (r, _) => r.IdempotencyKey.StartsWith("BF-") ? MfgRules.Fail(MfgErrors.Int001) : null;

        var result = await c.ReportAsync(order, 10, 60);

        result.IsPending.Should().BeTrue();
        result.Report.Status.Should().Be(OperationReportStatus.Pending);
        c.Op(order, 10).GoodQty.Should().Be(0m, "the report counts only when its materials are out");
        order.Status.Should().Be(OrderStatus.Released);
        var document = c.Documents.Docs.Single();
        document.Status.Should().Be(StockDocumentStatus.Pending);

        c.Stock.Intercept = null;
        await c.Posting.PostAsync(document, "job");
        await c.Posting.PostAsync(document, "job");          // a second round changes nothing

        document.Status.Should().Be(StockDocumentStatus.Posted);
        c.Reports.Items.Single().Status.Should().Be(OperationReportStatus.Posted);
        c.Op(order, 10).GoodQty.Should().Be(60m);
        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(960m);
        c.BackflushMovements("NVL-VIT01").Should().ContainSingle();
        order.Status.Should().Be(OrderStatus.InProgress);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready);
    }

    [Fact]
    public async Task A_pending_report_already_counts_toward_the_limits_and_the_cumulative_backflush_of_the_next()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        c.Stock.Intercept = (r, _) => r.IdempotencyKey.StartsWith("BF-") ? MfgRules.Fail(MfgErrors.Int001) : null;
        await c.ReportAsync(order, 10, 60);                                 // pending: 960 on its way

        (await Refused(() => c.ReportAsync(order, 10, 50))).RuleName.Should().Be(MfgErrors.Op004);   // 105 - 60 = 45 left
        await c.ReportAsync(order, 10, 40);

        var lines = c.Documents.Docs.Select(d => d.Lines.Single().Qty).ToList();
        lines.Should().Equal(960m, 640m);                                   // 1 600 in total, the pending 960 not taken twice
    }

    [Fact]
    public async Task A_refusal_on_the_second_line_reverses_the_first_and_fails_the_report()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        await c.ReportAsync(order, 10, 100);
        await c.ReportAsync(order, 20, 100);
        var xop = c.Items["NVL-XOP01"].Id;
        var thung = c.Items["NVL-THUNG01"].Id;
        c.Stock.Intercept = (r, _) => r.ProductId == xop && r.IdempotencyKey.StartsWith("BF-") ? new BusinessRuleException("WH_INSUFFICIENT_STOCK", "no foam") : null;

        var ex = await Refused(() => c.ReportAsync(order, 30, 50));

        ex.RuleName.Should().Be(MfgErrors.Mat008);
        c.Stock.Total(thung).Should().Be(100m, "the first line was booked and reversed");
        c.Op(order, 30).GoodQty.Should().Be(0m);
        var report = c.Reports.Items.Last();
        (report.Status, report.FailureCode).Should().Be((OperationReportStatus.Failed, MfgErrors.Mat008));
        var document = c.Documents.Docs.Last();
        document.Status.Should().Be(StockDocumentStatus.Failed);
        document.Lines.Select(l => l.Status).Should().Equal(StockLineStatus.Compensated, StockLineStatus.Refused);
        c.Mat(order, "NVL-THUNG01").ConsumedQty.Should().Be(0m);
    }

    [Fact]
    public async Task Abandoning_the_pending_backflush_fails_its_report()
    {
        var c = new OpsCtx();
        var order = await c.ReleasedAsync();
        c.Stock.Intercept = (r, _) => r.IdempotencyKey.StartsWith("BF-") ? MfgRules.Fail(MfgErrors.Int001) : null;
        await c.ReportAsync(order, 10, 60);
        c.Stock.Intercept = null;
        var document = c.Documents.Docs.Single();

        var abandoned = await new AbandonStockDocumentHandler(c.Access, c.Documents, c.Posting, c.User, c.F.Uow).Handle(new AbandonStockDocumentCommand(document.Id), default);

        abandoned.Document.Status.Should().Be(StockDocumentStatus.Failed);
        c.Reports.Items.Single().Should().Match<OperationReport>(r => r.Status == OperationReportStatus.Failed && r.FailureCode == "ABANDONED");
        c.Op(order, 10).GoodQty.Should().Be(0m);
    }

    [Theory]
    [InlineData("WH_INSUFFICIENT_STOCK", StockDocumentType.Backflush, MfgErrors.Mat008)]
    [InlineData("WH_INSUFFICIENT_STOCK", StockDocumentType.Issue, MfgErrors.Mat003)]
    [InlineData("WH_LOT_EXPIRED", StockDocumentType.Backflush, MfgErrors.Mat005)]
    public void The_code_mapper_knows_a_backflush_shortage(string code, StockDocumentType type, string expected)
        => ERP.MFG.Application.Services.StockCodeMapper.ToMfg(code, type).Should().Be(expected);
}
