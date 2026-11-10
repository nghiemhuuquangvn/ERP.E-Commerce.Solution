using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Events;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

public class MaterialLimitsTests
{
    [Theory]
    [InlineData(10, 3, 10.30)]
    [InlineData(52.5, 3, 54.075)]
    [InlineData(100, 0, 100)]
    public void Max_net_issue_is_required_times_tolerance(double required, double pct, double max)
        => MaterialLimits.MaxNetIssue((decimal)required, (decimal)pct).Should().Be((decimal)max);

    [Fact]
    public void The_boundary_is_inclusive_and_pending_issues_count()
    {
        MaterialLimits.ExceedsTolerance(10m, 3m, 0m, 0m, 10.30m).Should().BeFalse();   // TC-MAT-004
        MaterialLimits.ExceedsTolerance(10m, 3m, 0m, 0m, 10.31m).Should().BeTrue();    // TC-MAT-005
        MaterialLimits.ExceedsTolerance(10m, 3m, 5m, 5m, 0.31m).Should().BeTrue();
        MaterialLimits.RemainingWithinTolerance(10m, 3m, 10m, 0.2m).Should().Be(0.1m);
        MaterialLimits.RemainingWithinTolerance(10m, 3m, 11m, 0m).Should().Be(0m);
    }

    [Fact]
    public void Returns_are_limited_by_what_is_out_and_what_is_already_waiting()
    {
        MaterialLimits.MaxReturn(10m, 2m, 3m).Should().Be(5m);
        MaterialLimits.MaxReturn(10m, 10m, 0m).Should().Be(0m);
    }

    [Fact]
    public void Return_cost_is_the_weighted_average_of_the_issues_with_a_known_cost()
    {
        MaterialLimits.AverageIssueCost([(30m, 4m), (20m, 6m), (5m, null)]).Should().Be(4.8m);
        MaterialLimits.AverageIssueCost([(5m, null)]).Should().BeNull();
        MaterialLimits.AverageIssueCost([]).Should().BeNull();
    }
}

public class StockCodeMapperTests
{
    [Theory]
    [InlineData("WH_INSUFFICIENT_STOCK", "MFG_MAT_003")]
    [InlineData("WH_INSUFFICIENT_IN_WAREHOUSE", "MFG_MAT_003")]
    [InlineData("WH_LOT_EXPIRED", "MFG_MAT_005")]
    [InlineData("WH_LOT_NOT_USABLE", "MFG_MAT_005")]
    [InlineData("WH_LOT_NOT_AVAILABLE", "MFG_MAT_005")]
    [InlineData("WH_LOT_REQUIRED", "MFG_MAT_004")]
    [InlineData("WH_SERIAL_NOT_SUPPORTED", "MFG_MAT_011")]
    [InlineData("WH_SOMETHING_ELSE", "WH_SOMETHING_ELSE")]
    public void Warehouse_codes_become_the_codes_of_the_spec(string warehouse, string expected) => StockCodeMapper.ToMfg(warehouse).Should().Be(expected);

    [Fact]
    public void Reservation_problems_and_outages_are_told_apart_from_refusals()
    {
        StockCodeMapper.IsReservationProblem("WH_RESERVATION_EXCEEDED").Should().BeTrue();
        StockCodeMapper.IsReservationProblem("WH_INSUFFICIENT_STOCK").Should().BeFalse();
        StockCodeMapper.IsUnavailable(MfgErrors.Int001).Should().BeTrue();
        StockCodeMapper.IsUnavailable("WH_CONFLICT").Should().BeTrue();
        StockCodeMapper.IsUnavailable("WH_LOT_EXPIRED").Should().BeFalse();
    }

    [Fact]
    public void Back_off_doubles_from_ten_seconds_up_to_five_minutes()
    {
        StockRetryPolicy.Backoff(0).Should().Be(TimeSpan.FromSeconds(10));
        StockRetryPolicy.Backoff(1).Should().Be(TimeSpan.FromSeconds(20));
        StockRetryPolicy.Backoff(3).Should().Be(TimeSpan.FromSeconds(80));
        StockRetryPolicy.Backoff(50).Should().Be(TimeSpan.FromMinutes(5));
    }
}

public class MaterialIssueTests
{
    [Fact] // TC-MAT-001, TC-PO-018
    public async Task An_issue_is_posted_updates_the_order_and_starts_it()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        var keo = c.Items["NVL-KEO01"].Id;

        var result = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10)]);

        result.IsPending.Should().BeFalse();
        result.Document.Status.Should().Be(StockDocumentStatus.Posted);
        result.Document.DocNo.Should().Be("MI-2611-00001");
        result.Document.Lines.Should().ContainSingle(l => l.IsFromReservation && l.MovementKey == "MI-2611-00001#1" && l.Status == StockLineStatus.Posted && l.Qty == 10m);
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(10m);
        order.Status.Should().Be(OrderStatus.InProgress);
        order.ActualStart.Should().NotBeNull();
        c.Stock.Total(keo).Should().Be(90m);
        c.Stock.Requests.Single().ReservationId.Should().NotBeNull();
        order.ReservationSyncPending.Should().BeFalse();
        c.Documents.Docs.Single().DomainEvents.OfType<MaterialIssuedEvent>().Should().ContainSingle(e => e.Lines.Single().Quantity == 10m);
    }

    [Fact] // R234
    public async Task A_material_booked_by_backflush_cannot_be_issued_by_hand_and_nothing_is_stored_or_sent()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        var vitOnly = () => c.IssueAsync(order, [c.Line(order, "NVL-VIT01", 100)]);
        var refusal = (await vitOnly.Should().ThrowAsync<BusinessRuleException>()).Which;
        refusal.RuleName.Should().Be(MfgErrors.Mat013);
        refusal.Message.Should().Contain("NVL-VIT01");

        var mixed = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1), c.Line(order, "NVL-VIT01", 100)]);   // the whole request is refused
        (await mixed.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat013);
        c.Documents.Docs.Should().BeEmpty();
        c.Stock.Movements.Should().BeEmpty();
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(0m);
    }

    [Fact] // TC-MAT-002 / 003 / PO-020
    public async Task Only_a_released_or_running_order_takes_material()
    {
        var c = new StockCtx();
        var draft = await c.DraftAsync();
        var order = c.Stored(draft.Id);

        var onDraft = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1)]);
        (await onDraft.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat001);

        await c.ReleaseAsync(order.Id);
        await c.HoldAsync(order.Id, c.HoldReason.Id);
        var onHold = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1)]);
        (await onHold.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po011);

        await c.ResumeAsync(order.Id);
        order.TransitionTo(OrderStatus.InProgress, "t");
        order.TransitionTo(OrderStatus.Completed, "t");
        var onCompleted = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1)]);
        (await onCompleted.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat001);
        c.Documents.Docs.Should().BeEmpty();
    }

    [Fact] // TC-MAT-004
    public async Task The_tolerance_boundary_is_accepted()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        var result = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10.30m)]);

        result.Document.Status.Should().Be(StockDocumentStatus.Posted);
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(10.30m);
    }

    [Fact] // TC-MAT-005 / 006 / TC-SET-003
    public async Task Beyond_the_tolerance_needs_the_permission_and_an_over_issue_reason()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        var plain = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10.31m)]);
        (await plain.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat002);

        var manager = StockCtx.UserWith(PermissionConstants.Manufacturing_MaterialOverIssue);
        var noReason = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 12m)], user: manager);
        (await noReason.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat002);
        var wrongReason = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 12m)], c.HoldReason.Id, user: manager);
        (await wrongReason.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat002);
        var noPermission = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 12m)], c.OverIssueReason.Id);
        (await noPermission.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat002);

        var ok = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 12m)], c.OverIssueReason.Id, user: manager);

        ok.Document.Status.Should().Be(StockDocumentStatus.Posted);
        ok.Document.ReasonCodeId.Should().Be(c.OverIssueReason.Id);
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(12m);
        c.Documents.Docs.Should().ContainSingle();   // the refused attempts stored nothing
    }

    [Fact]
    public async Task A_changed_tolerance_applies_from_then_on()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        c.PlantSettings.OverIssueTolerancePct = 0m;

        var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10.30m)]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat002);
    }

    [Fact] // TC-MAT-007
    public async Task More_than_the_stock_is_refused_before_anything_is_stored()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        var act = () => c.IssueAsync(order, [c.Line(order, "BTP-CHAN01", 350)]);   // stock 300, all of it reserved for this order

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat003);
        c.Documents.Docs.Should().BeEmpty();
        c.Stock.MovementCalls.Should().Be(0);
    }

    [Fact]
    public async Task What_the_order_itself_reserved_counts_as_available_to_it()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        var result = await c.IssueAsync(order, [c.Line(order, "BTP-CHAN01", 300)]);   // exactly the reservation

        result.Document.Status.Should().Be(StockDocumentStatus.Posted);
        c.Stock.Total(c.Items["BTP-CHAN01"].Id).Should().Be(0m);
    }

    [Fact]
    public async Task A_line_above_the_reservation_takes_the_rest_from_free_stock_in_a_second_movement()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        var result = await c.IssueAsync(order, [c.Line(order, "NVL-SON01", 54m)]);   // reserved 52.5, tolerance 54.075

        var lines = result.Document.Lines;
        lines.Should().HaveCount(2);
        lines[0].Should().Match<StockDocumentLineDto>(l => l.IsFromReservation && l.Qty == 52.5m && l.MovementKey == "MI-2611-00001#1");
        lines[1].Should().Match<StockDocumentLineDto>(l => !l.IsFromReservation && l.Qty == 1.5m && l.MovementKey == "MI-2611-00001#2F");
        c.Stock.Requests.Select(r => r.ReservationId.HasValue).Should().Equal(true, false);
        c.Stock.Total(c.Items["NVL-SON01"].Id).Should().Be(46m);
        c.Mat(order, "NVL-SON01").IssuedQty.Should().Be(54m);
    }

    [Fact] // TC-MAT-008
    public async Task A_lot_controlled_item_must_name_a_lot_for_stock_outside_the_reservation()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        c.Replace("NVL-KEO01", p => p with { IsLotControlled = true });
        c.Stock.Balances.Remove(c.Items["NVL-KEO01"].Id);
        c.Stock.Lot(c.Items["NVL-KEO01"].Id, c.RawWarehouse, "A", 100);

        var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 5)]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat004);
    }

    [Fact] // TC-MAT-009 / 011
    public async Task An_expired_held_or_unknown_lot_is_refused()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        var keo = c.Items["NVL-KEO01"].Id;
        c.Replace("NVL-KEO01", p => p with { IsLotControlled = true });
        c.Stock.Balances.Remove(keo);
        var expired = c.Stock.Lot(keo, c.RawWarehouse, "X", 20, expiry: new DateTime(2026, 10, 31));
        var held = c.Stock.Lot(keo, c.RawWarehouse, "Q", 20, status: "QcHold");

        foreach (var lot in new[] { expired, held, Guid.NewGuid() })
        {
            var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 5, lot)]);
            (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat005);
        }
    }

    [Fact]
    public async Task A_named_lot_is_issued_from_that_lot()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        var keo = c.Items["NVL-KEO01"].Id;
        c.Replace("NVL-KEO01", p => p with { IsLotControlled = true });
        c.Stock.Balances.Remove(keo);
        var lotA = c.Stock.Lot(keo, c.RawWarehouse, "A", 4);
        var lotB = c.Stock.Lot(keo, c.RawWarehouse, "B", 50);

        var result = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 4, lotA), c.Line(order, "NVL-KEO01", 6, lotB)]);

        result.Document.Lines.Select(l => (l.LotId, l.Qty)).Should().BeEquivalentTo(new[] { ((Guid?)lotA, 4m), ((Guid?)lotB, 6m) });
        c.Stock.Requests.All(r => r.RequiresLot).Should().BeTrue();
    }

    [Fact] // TC-MAT-012 (serial variant)
    public async Task A_serial_controlled_item_is_refused()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        c.Replace("NVL-KEO01", p => p with { IsSerialControlled = true });

        var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1)]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat011);
    }

    [Fact]
    public async Task Quantities_follow_the_decimals_of_the_unit_and_must_be_positive()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        foreach (var qty in new[] { 0m, -1m, 5.005m })
        {
            var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", qty)]);
            await act.Should().ThrowAsync<BusinessRuleException>();
        }
        var unknown = () => c.IssueAsync(order, [new MaterialLineBody(99, 1m, null)]);
        await unknown.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact] // TC-MAT-013
    public async Task One_document_carries_several_materials()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        var result = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10), c.Line(order, "NVL-SON01", 52.5m), c.Line(order, "BTP-MAT01", 100)]);

        result.Document.Lines.Should().HaveCount(3);
        c.Documents.Docs.Should().ContainSingle();
        c.Stock.Movements.Should().HaveCount(3);
        order.Materials.Sum(m => m.IssuedQty).Should().Be(162.5m);
    }

    [Fact]
    public async Task Two_lines_for_the_same_material_and_lot_are_merged()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();

        var result = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 4), c.Line(order, "NVL-KEO01", 6)]);

        result.Document.Lines.Should().ContainSingle(l => l.Qty == 10m);
    }

    [Fact]
    public async Task A_pending_sync_is_finished_first_and_a_stuck_one_refuses_the_issue()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        order.MarkReservationSync();
        c.Stock.Down = true;

        var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1)]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat012);
        c.Documents.Docs.Should().BeEmpty();

        c.Stock.Down = false;
        var ok = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1)]);
        ok.Document.Status.Should().Be(StockDocumentStatus.Posted);
    }
}

public class StockPostingTests
{
    private static async Task<(StockCtx C, ProductionOrder Order)> TwoLineScenarioAsync()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        return (c, order);
    }

    [Fact] // the transient window: Pending, then the job posts it exactly once
    public async Task A_warehouse_outage_on_the_second_line_leaves_the_document_pending_and_the_next_round_finishes_it_once()
    {
        var (c, order) = await TwoLineScenarioAsync();
        var keo = c.Items["NVL-KEO01"].Id;
        c.Stock.Intercept = (_, call) => call == 2 ? MfgRules.Fail(MfgErrors.Int001) : null;

        var first = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10), c.Line(order, "NVL-SON01", 5)]);

        first.IsPending.Should().BeTrue();
        var doc = c.Documents.Docs.Single();
        doc.Status.Should().Be(StockDocumentStatus.Pending);
        doc.Attempts.Should().Be(1);
        doc.NextAttemptAt.Should().BeAfter(c.Clock.Now.UtcDateTime);
        doc.Lines.Select(l => l.Status).Should().Equal(StockLineStatus.Posted, StockLineStatus.Pending);
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(0m);          // the order shows only what the Warehouse has fully booked
        order.Status.Should().Be(OrderStatus.Released);
        (await new OrderTransactionGuard(c.Documents).HasTransactionsAsync(order)).Should().BeTrue();

        c.Stock.Intercept = null;
        var second = await c.Posting.PostAsync(doc, "job");

        second.Status.Should().Be(StockDocumentStatus.Posted);
        c.Stock.Movements.Count(m => m.Key == "MI-2611-00001#1").Should().Be(1);   // the first line was replayed, not booked twice
        c.Stock.Total(keo).Should().Be(90m);
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(10m);
        c.Mat(order, "NVL-SON01").IssuedQty.Should().Be(5m);
        order.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact] // TC-INT-002
    public async Task A_refusal_on_the_second_line_reverses_the_first_and_fails_the_document()
    {
        var (c, order) = await TwoLineScenarioAsync();
        var keo = c.Items["NVL-KEO01"].Id;
        c.Stock.Intercept = (r, _) => r.ProductId == c.Items["NVL-SON01"].Id ? new BusinessRuleException("WH_LOT_EXPIRED", "Lot expired.") : null;

        var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10), c.Line(order, "NVL-SON01", 5)]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat005);
        var doc = c.Documents.Docs.Single();
        doc.Status.Should().Be(StockDocumentStatus.Failed);
        doc.LastErrorCode.Should().Be(MfgErrors.Mat005);
        doc.Lines.Select(l => l.Status).Should().Equal(StockLineStatus.Compensated, StockLineStatus.Refused);
        c.Stock.Movements.Select(m => (m.Key, m.Type)).Should().Contain(("MI-2611-00001#1R", "ProductionReturn"));
        c.Stock.Total(keo).Should().Be(100m);                           // booked then reversed
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(0m);
        order.Status.Should().Be(OrderStatus.Released);
    }

    [Fact]
    public async Task The_failed_document_is_final_and_does_not_block_the_order()
    {
        var (c, order) = await TwoLineScenarioAsync();
        c.Stock.Intercept = (_, _) => new BusinessRuleException("WH_INSUFFICIENT_STOCK", "short");
        var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10)]);
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat003);
        c.Stock.Intercept = null;

        (await c.Posting.PostAsync(c.Documents.Docs.Single(), "job")).Status.Should().Be(StockDocumentStatus.Failed);
        (await new OrderTransactionGuard(c.Documents).HasTransactionsAsync(order)).Should().BeFalse();
        await c.UnreleaseAsync(order.Id);                               // nothing is pending, nothing was issued
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task A_reservation_that_moved_makes_the_line_come_from_free_stock_under_a_free_key()
    {
        var (c, order) = await TwoLineScenarioAsync();
        c.Stock.Intercept = (r, _) => r.ReservationId.HasValue ? new BusinessRuleException("WH_RESERVATION_NOT_OPEN", "gone") : null;

        var result = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10)]);

        result.Document.Status.Should().Be(StockDocumentStatus.Posted);
        result.Document.Lines.Single().Should().Match<StockDocumentLineDto>(l => !l.IsFromReservation && l.MovementKey == "MI-2611-00001#1F");
        c.Stock.Movements.Should().ContainSingle(m => m.Key == "MI-2611-00001#1F");
    }

    [Fact]
    public async Task A_key_conflict_from_the_warehouse_is_a_failure_not_a_replay()
    {
        var (c, order) = await TwoLineScenarioAsync();
        c.Stock.Intercept = (_, _) => new ConcurrencyException("WH_IDEMPOTENCY_CONFLICT", "other quantity");

        var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10)]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_IDEMPOTENCY_CONFLICT");
        c.Documents.Docs.Single().Status.Should().Be(StockDocumentStatus.Failed);
    }

    [Fact]
    public async Task A_plain_conflict_without_a_code_keeps_the_document_pending()
    {
        var (c, order) = await TwoLineScenarioAsync();
        c.Stock.Intercept = (_, call) => call == 1 ? new ConcurrencyException("WH_CONFLICT", "busy") : null;

        var result = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10)]);

        result.IsPending.Should().BeTrue();
        c.Documents.Docs.Single().Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Abandoning_a_pending_document_reverses_what_it_booked()
    {
        var (c, order) = await TwoLineScenarioAsync();
        var keo = c.Items["NVL-KEO01"].Id;
        c.Stock.Intercept = (_, call) => call == 2 ? MfgRules.Fail(MfgErrors.Int001) : null;
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10), c.Line(order, "NVL-SON01", 5)]);
        c.Stock.Intercept = null;
        var doc = c.Documents.Docs.Single();
        var user = StockCtx.UserWith(PermissionConstants.Manufacturing_StockDocumentRetry);

        var result = await new AbandonStockDocumentHandler(c.Access, c.Documents, c.Posting, user, c.F.Uow).Handle(new AbandonStockDocumentCommand(doc.Id), default);

        result.Document.Status.Should().Be(StockDocumentStatus.Failed);
        result.Document.LastErrorCode.Should().Be("ABANDONED");
        doc.Lines.Select(l => l.Status).Should().Equal(StockLineStatus.Compensated, StockLineStatus.Refused);
        c.Stock.Total(keo).Should().Be(100m);
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(0m);
    }

    [Fact]
    public async Task A_rollback_that_cannot_reach_the_warehouse_stays_pending_in_rollback_mode_and_finishes_later()
    {
        var (c, order) = await TwoLineScenarioAsync();
        var keo = c.Items["NVL-KEO01"].Id;
        c.Stock.Intercept = (_, call) => call == 2 ? MfgRules.Fail(MfgErrors.Int001) : null;
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10), c.Line(order, "NVL-SON01", 5)]);
        var doc = c.Documents.Docs.Single();
        doc.Abandon("tester");
        c.Stock.Down = true;

        (await c.Posting.PostAsync(doc, "job")).Status.Should().Be(StockDocumentStatus.Pending);
        doc.Mode.Should().Be(StockDocumentMode.Rollback);

        c.Stock.Down = false;
        c.Stock.Intercept = null;
        (await c.Posting.PostAsync(doc, "job")).Status.Should().Be(StockDocumentStatus.Failed);
        c.Stock.Total(keo).Should().Be(100m);
    }

    [Fact] // TC-PO-017 with a pending document
    public async Task A_pending_document_blocks_unrelease_and_cancel()
    {
        var (c, order) = await TwoLineScenarioAsync();
        c.Stock.Down = true;
        // the release check passed earlier; now the Warehouse goes away mid-issue
        c.Stock.Down = false;
        c.Stock.Intercept = (_, _) => MfgRules.Fail(MfgErrors.Int001);
        var result = await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10)]);
        result.IsPending.Should().BeTrue();
        c.Stock.Intercept = null;

        var unrelease = () => c.UnreleaseAsync(order.Id);
        (await unrelease.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po008);
        var cancel = () => c.CancelAsync(order.Id, c.CancelReason.Id);
        (await cancel.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po008);

        // the order screen shows the count
        var dto = await new GetOrderHandler(c.Access, c.Repo, c.Documents).Handle(new GetOrderQuery(order.Id), default);
        dto.PendingStockDocuments.Should().Be(1);
    }

    [Fact] // TC-GEN-010
    public async Task The_same_idempotency_key_returns_the_same_document()
    {
        var (c, order) = await TwoLineScenarioAsync();
        var lines = new[] { c.Line(order, "NVL-KEO01", 10) };

        var first = await c.IssueAsync(order, lines, key: "client-key-1");
        var second = await c.IssueAsync(order, lines, key: "client-key-1");

        second.Document.Id.Should().Be(first.Document.Id);
        c.Documents.Docs.Should().ContainSingle();
        c.Stock.Movements.Should().ContainSingle();
        c.Mat(order, "NVL-KEO01").IssuedQty.Should().Be(10m);

        var other = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 5)], key: "client-key-1");
        (await other.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Gen007);
    }

    [Fact]
    public async Task A_pending_issue_counts_against_the_tolerance_of_the_next_one()
    {
        var (c, order) = await TwoLineScenarioAsync();
        c.Stock.Intercept = (_, _) => MfgRules.Fail(MfgErrors.Int001);
        (await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 10)])).IsPending.Should().BeTrue();
        c.Stock.Intercept = null;

        var act = () => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 0.5m)]);   // 10 pending + 0.5 > 10.30

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat002);
    }
}

public class MaterialReturnTests
{
    private static async Task<(StockCtx C, ProductionOrder Order)> IssuedAsync(decimal keo = 10m)
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        c.Stock.Costs[c.Items["NVL-KEO01"].Id] = 4m;
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", keo)]);
        return (c, order);
    }

    [Fact] // TC-MAT-014, 017
    public async Task A_return_goes_back_at_the_issue_cost_and_does_not_reserve_again()
    {
        var (c, order) = await IssuedAsync();
        var keo = c.Items["NVL-KEO01"].Id;

        var result = await c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 2)]);

        result.Document.Should().Match<StockDocumentDto>(d => d.Status == StockDocumentStatus.Posted && d.Type == StockDocumentType.Return && d.DocNo == "MR-2611-00001");
        var request = c.Stock.Requests.Last();
        request.MovementType.Should().Be("ProductionReturn");
        request.UnitCost.Should().Be(4m);
        request.ReservationId.Should().BeNull();
        c.Mat(order, "NVL-KEO01").ReturnedQty.Should().Be(2m);
        c.Mat(order, "NVL-KEO01").NetIssuedQty.Should().Be(8m);
        c.Stock.Total(keo).Should().Be(92m);
        c.Documents.Docs.Last().DomainEvents.OfType<MaterialReturnedEvent>().Should().ContainSingle();
    }

    [Fact] // TC-MAT-016
    public async Task A_return_cannot_exceed_what_is_out()
    {
        var (c, order) = await IssuedAsync();

        var tooMuch = () => c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 10.01m)]);
        (await tooMuch.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat006);

        (await c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 10)])).Document.Status.Should().Be(StockDocumentStatus.Posted);
        var again = () => c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 0.01m)]);
        (await again.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat006);
    }

    [Fact]
    public async Task Returns_that_are_still_pending_count_against_the_limit()
    {
        var (c, order) = await IssuedAsync();
        c.Stock.Intercept = (_, _) => MfgRules.Fail(MfgErrors.Int001);
        (await c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 8)])).IsPending.Should().BeTrue();
        c.Stock.Intercept = null;

        var act = () => c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 3)]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat006);
        (await c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 2)])).IsPending.Should().BeFalse();
    }

    [Fact] // TC-MAT-015
    public async Task A_lot_controlled_return_names_its_lot_and_is_capped_by_that_lot()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        var keo = c.Items["NVL-KEO01"].Id;
        c.Replace("NVL-KEO01", p => p with { IsLotControlled = true });
        c.Stock.Balances.Remove(keo);
        var lotA = c.Stock.Lot(keo, c.RawWarehouse, "A", 50);
        var lotB = c.Stock.Lot(keo, c.RawWarehouse, "B", 50);
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 4, lotA), c.Line(order, "NVL-KEO01", 6, lotB)]);

        var noLot = () => c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 1)]);
        (await noLot.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat004);
        var overLot = () => c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 5, lotA)]);   // only 4 came from lot A although 10 are out
        (await overLot.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat006);

        var ok = await c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 4, lotA)]);

        ok.Document.Lines.Single().LotId.Should().Be(lotA);
        c.Stock.Balances[keo].Single(b => b.LotId == lotA).Quantity.Should().Be(50m);   // 50 - 4 + 4
    }

    [Fact]
    public async Task A_return_on_a_held_order_is_refused()
    {
        var (c, order) = await IssuedAsync();
        await c.HoldAsync(order.Id, c.HoldReason.Id);

        var act = () => c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 1)]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po011);
    }

    [Fact]
    public async Task The_cost_of_a_return_is_the_average_of_the_issues_of_that_material()
    {
        var c = new StockCtx();
        var order = await c.ReleasedAsync();
        var keo = c.Items["NVL-KEO01"].Id;
        c.Stock.Costs[keo] = 4m;
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 6)]);
        c.Stock.Costs[keo] = 6m;
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 4)]);

        await c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 1)]);

        c.Stock.Requests.Last().UnitCost.Should().Be(4.8m);
    }
}
