using ERP.MFG.Application.Features.Operations;
using ERP.MFG.Application.Features.Receipts;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>The pure parts of a reversal (P4-2): the lines it is made of and the two checks that need no data.</summary>
public class ReversalPlannerTests
{
    private static readonly Guid Order = Guid.NewGuid();
    private static readonly Guid Material = Guid.NewGuid();
    private static readonly Guid Item = Guid.NewGuid();
    private static readonly Guid Wh = Guid.NewGuid();
    private static readonly Guid LotA = Guid.NewGuid();
    private static readonly Guid LotB = Guid.NewGuid();

    private static MaterialTransaction Posted(StockDocumentType type, IReadOnlyList<NewStockLine> lines, Func<MaterialTransactionLine, IReadOnlyList<LineLot>?> lots, Func<MaterialTransactionLine, Guid?> lotId)
    {
        var document = MaterialTransaction.Create("BF-2611-00001", type, Guid.NewGuid(), Order, "MO-2611-00001", new DateOnly(2026, 11, 4), null, null, null, null, lines, "t");
        foreach (var line in document.PendingLines.ToList()) document.RecordLinePosted(line, Guid.NewGuid(), 4m, line.Qty * 4m, lotId(line), lots(line));
        document.MarkPosted("t", DateTime.UtcNow);
        return document;
    }

    private static NewStockLine Out(decimal qty, bool reserved = true) => new(Material, 1, Item, "NVL-SON01", Wh, null, qty, reserved, null);

    [Fact]
    public void A_multi_lot_line_goes_back_lot_by_lot_at_the_cost_it_left_with_and_the_lot_less_part_goes_back_lot_less()
    {
        var original = Posted(StockDocumentType.Backflush, [Out(52.5m)],
            _ => [new LineLot(LotA, "L-SON-A", 30m, 4.5m, Wh, null), new LineLot(LotB, "L-SON-B", 20m, 5m, Wh, null)], _ => null);

        var lines = ReversalPlanner.ReturnMaterial(original);

        lines.Select(l => (l.LotId, l.Qty, l.UnitCost, l.Action)).Should().Equal(
            (LotA, 30m, 4.5m, ReversalAction.Return), (LotB, 20m, 5m, ReversalAction.Return), ((Guid?)null, 2.5m, 4m, ReversalAction.Return));
        lines.Should().OnlyContain(l => l.OrderMaterialId == Material && l.Kind == StockLineKind.Material && l.ReversesLineId == original.Lines.Single().Id && !l.IsFromReservation);
        lines.Sum(l => l.Qty).Should().Be(52.5m, "everything that was consumed comes back");
    }

    [Fact]
    public void A_line_without_stored_lots_goes_back_to_the_lot_it_was_booked_in()
    {
        var original = Posted(StockDocumentType.Backflush, [Out(10m, reserved: false)], _ => null, _ => LotA);

        var line = ReversalPlanner.ReturnMaterial(original).Single();

        (line.LotId, line.Qty, line.UnitCost, line.WarehouseId).Should().Be((LotA, 10m, 4m, Wh));
    }

    [Fact]
    public void A_receipt_line_is_scrapped_from_its_lot_or_issued_lot_less_and_a_material_line_comes_back()
    {
        var output = Guid.NewGuid();
        var lines = new List<NewStockLine>
        {
            Out(8m, reserved: false),
            NewStockLine.Receipt(Item, "TP-BAN01", Wh, 40m, null, null, "TP-BAN01-261104-01", null, null, true),
            NewStockLine.Receipt(Guid.NewGuid(), "PP-MUN01", Wh, 20m, output, 1, null, null, null, false),
        };
        var original = Posted(StockDocumentType.Receipt, lines, l => l.Kind == StockLineKind.Receipt && l.LotNumber != null ? [new LineLot(LotA, l.LotNumber, l.Qty, 4m, Wh, null)] : null,
            l => l.Kind == StockLineKind.Receipt && l.LotNumber != null ? LotA : null);

        var takeOut = ReversalPlanner.TakeOutReceipt(original);
        var giveBack = ReversalPlanner.ReturnMaterial(original);

        takeOut.Select(l => (l.ItemCode, l.Qty, l.LotId, l.Action, l.OrderOutputId, l.IsQcHold, l.LotNumber)).Should().Equal(
            ("TP-BAN01", 40m, (Guid?)LotA, (ReversalAction?)ReversalAction.Scrap, (Guid?)null, false, "TP-BAN01-261104-01"),
            ("PP-MUN01", 20m, null, ReversalAction.Issue, output, false, null));
        takeOut.Should().OnlyContain(l => l.Kind == StockLineKind.Receipt);
        giveBack.Should().ContainSingle().Which.Should().Match<NewStockLine>(l => l.Qty == 8m && l.Action == ReversalAction.Return);
    }

    [Fact]
    public void Only_posted_lines_are_reversed()
    {
        var document = MaterialTransaction.Create("BF-2611-00002", StockDocumentType.Backflush, Guid.NewGuid(), Order, "MO", new DateOnly(2026, 11, 4), null, null, null, null, [Out(5m), Out(6m)], "t");
        document.RecordLinePosted(document.Lines[0], Guid.NewGuid(), 4m, 20m, null);
        document.RefuseLine(document.Lines[1], "WH_X", "no");

        ReversalPlanner.ReturnMaterial(document).Should().ContainSingle().Which.Qty.Should().Be(5m);
    }

    [Theory]
    [InlineData(60, 60, 0, true)]      // nothing used downstream: all of it can go
    [InlineData(60, 60, 50, false)]    // TC-REV-002: the next operation took 50
    [InlineData(100, 60, 40, true)]    // 40 left covers the 40 used
    [InlineData(100, 60, 41, false)]
    public void A_report_can_go_when_what_is_left_still_covers_what_downstream_took(decimal good, decimal reversed, decimal used, bool covered)
        => ReversalChecks.ReportCovered(good, reversed, used).Covered.Should().Be(covered);

    [Fact]
    public void A_lot_that_was_used_or_reserved_is_named_in_the_reason()
    {
        ReversalChecks.LotProblem("L-1", 40m, 40m, 0m).Should().BeNull();
        ReversalChecks.LotProblem("L-1", 40m, 30m, 0m).Should().Contain("L-1").And.Contain("30");            // TC-REV-006
        ReversalChecks.LotProblem("L-1", 40m, 40m, 5m).Should().Contain("giữ chỗ");
    }
}

public class ReversalTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> act) => (await act.Should().ThrowAsync<BusinessRuleException>()).Which;

    private static async Task<(OpsCtx C, ProductionOrder Order)> ReadyAsync(bool vitUnbound = false, bool lot = true, bool fqc = true)
    {
        var c = new OpsCtx { VitUnbound = vitUnbound };
        await c.SeedAllAsync();
        c.StockDs01();
        c.FinishedGoodIs(lot, fqc);
        var draft = await c.CreateOrderAsync("TP-BAN01", 100);
        await c.ReleaseAsync(draft.Id);
        return (c, c.Stored(draft.Id));
    }

    // ── Operation report (BR-OP-07) ────────────────────────────────────────────────────────────────────────

    [Fact] // TC-REV-001
    public async Task Reversing_a_report_nobody_used_gives_back_the_screws_and_takes_the_quantities_off()
    {
        var (c, order) = await ReadyAsync();
        var vit = c.Items["NVL-VIT01"].Id;
        var report = (await c.ReportAsync(order, 10, 60, scrap: 2)).Report;
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready);
        c.Stock.Total(vit).Should().Be(1600m - 992m);

        var result = await c.ReverseReportAsync(order, report.Id);

        result.IsPending.Should().BeFalse();
        result.Report.Should().Match<OperationReportDto>(r => r.ReversalOfId == report.Id && r.Status == OperationReportStatus.Posted && r.GoodQty == 60m && r.ScrapQty == 2m);
        result.Backflush!.Should().Match<StockDocumentDto>(d => d.Type == StockDocumentType.Reversal && d.Status == StockDocumentStatus.Posted && d.DocNo == "RV-2611-00001" && d.ReversalOfId != null);
        result.Backflush.Lines.Should().ContainSingle().Which.Should().Match<StockDocumentLineDto>(l => l.Qty == 992m && l.ReversalAction == ReversalAction.Return && l.MovementKey == "RV-2611-00001#1");
        (c.Op(order, 10).GoodQty, c.Op(order, 10).ScrapQty, order.ScrappedQty).Should().Be((0m, 0m, 0m));
        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(0m);
        c.Stock.Total(vit).Should().Be(1600m, "the screws are back in the Warehouse");
        c.Op(order, 20).Status.Should().Be(OperationStatus.Pending, "BR-OP-01 the other way round: no good output left on operation 10");
        var original = c.Reports.Items.Single(r => r.Id == report.Id);
        original.IsReversed.Should().BeTrue();
        c.Documents.Docs.Single(d => d.Type == StockDocumentType.Backflush).IsReversed.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact] // TC-REV-002
    public async Task A_report_whose_output_the_next_operation_used_cannot_be_reversed()
    {
        var (c, order) = await ReadyAsync();
        var first = (await c.ReportAsync(order, 10, 60)).Report;
        await c.ReportAsync(order, 20, 50);
        var movements = c.Stock.MovementCalls;

        var ex = await Refused(() => c.ReverseReportAsync(order, first.Id));

        ex.RuleName.Should().Be(MfgErrors.Op007);
        ex.Message.Should().Contain("50");
        c.Stock.MovementCalls.Should().Be(movements, "nothing was sent to the Warehouse");
        c.Documents.Docs.Should().NotContain(d => d.Type == StockDocumentType.Reversal);
        c.Reports.Items.Should().NotContain(r => r.ReversalOfId != null);
        c.Op(order, 10).GoodQty.Should().Be(60m);
    }

    [Fact] // TC-REV-003
    public async Task The_last_operations_good_output_that_was_received_cannot_be_reversed()
    {
        var (c, order) = await ReadyAsync();
        await c.ReportAsync(order, 10, 100);
        await c.ReportAsync(order, 20, 100);
        var last = (await c.ReportAsync(order, 30, 100)).Report;
        await c.ReceiveMainAsync(order, 40);

        var ex = await Refused(() => c.ReverseReportAsync(order, last.Id));

        ex.RuleName.Should().Be(MfgErrors.Op007);
        // after the receipt is reversed the report can go
        var receipt = c.Documents.Docs.Single(d => d.Type == StockDocumentType.Receipt);
        await c.ReverseReceiptAsync(order, receipt.Id);
        (await c.ReverseReportAsync(order, last.Id)).Report.Status.Should().Be(OperationReportStatus.Posted);
        c.Op(order, 30).GoodQty.Should().Be(0m);
    }

    [Fact]
    public async Task Scrap_and_rework_of_a_report_can_be_reversed_even_when_its_good_output_is_in_use()
    {
        var (c, order) = await ReadyAsync();
        await c.ReportAsync(order, 10, 60);
        var scrapOnly = (await c.ReportAsync(order, 10, 0, scrap: 3)).Report;
        await c.ReportAsync(order, 20, 60);

        await c.ReverseReportAsync(order, scrapOnly.Id);

        (c.Op(order, 10).GoodQty, c.Op(order, 10).ScrapQty, order.ScrappedQty).Should().Be((60m, 0m, 0m));
    }

    [Fact]
    public async Task A_report_without_backflush_is_reversed_without_the_warehouse()
    {
        var (c, order) = await ReadyAsync();
        await c.ReportAsync(order, 10, 60);
        var report = (await c.ReportAsync(order, 20, 40)).Report;
        report.BackflushDocumentId.Should().BeNull("operation 20 has no backflush material");
        c.Stock.Down = true;

        var result = await c.ReverseReportAsync(order, report.Id);

        result.IsPending.Should().BeFalse();
        result.Backflush.Should().BeNull();
        c.Op(order, 20).GoodQty.Should().Be(0m);
        c.Documents.Docs.Should().NotContain(d => d.Type == StockDocumentType.Reversal);
    }

    [Fact]
    public async Task A_completed_operation_goes_back_to_ready_when_its_input_is_no_longer_all_taken()
    {
        var (c, order) = await ReadyAsync();
        await c.ReportAsync(order, 10, 60);
        var last = (await c.ReportAsync(order, 10, 40)).Report;
        c.Op(order, 10).Status.Should().Be(OperationStatus.Completed);

        await c.ReverseReportAsync(order, last.Id);

        c.Op(order, 10).Status.Should().Be(OperationStatus.Ready);
        c.Op(order, 10).ActualEnd.Should().BeNull();
        c.Op(order, 10).GoodQty.Should().Be(60m);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready, "operation 10 still has good output");
    }

    [Fact]
    public async Task A_report_is_reversed_once_and_a_reversal_cannot_be_reversed()
    {
        var (c, order) = await ReadyAsync();
        var report = (await c.ReportAsync(order, 10, 60)).Report;
        var reversal = (await c.ReverseReportAsync(order, report.Id)).Report;

        (await Refused(() => c.ReverseReportAsync(order, report.Id))).RuleName.Should().Be(MfgErrors.Rev001);
        (await Refused(() => c.ReverseReportAsync(order, reversal.Id))).RuleName.Should().Be(MfgErrors.Rev001);
    }

    [Fact]
    public async Task A_report_needs_an_open_order_and_a_reason()
    {
        var (c, order) = await ReadyAsync();
        var report = (await c.ReportAsync(order, 10, 60)).Report;

        (await Refused(() => c.ReverseReportAsync(order, report.Id, note: " "))).RuleName.Should().Be("MFG_VALIDATION");
        (await Refused(() => c.ReverseReportAsync(order, report.Id, note: "ab"))).RuleName.Should().Be("MFG_VALIDATION");
        await c.HoldAsync(order.Id, c.HoldReason.Id);
        (await Refused(() => c.ReverseReportAsync(order, report.Id))).RuleName.Should().Be(MfgErrors.Po011);
        await c.ResumeAsync(order.Id);
        await c.ReportAsync(order, 10, 40);                                  // finishes the route; reverse nothing yet
        await c.ReportAsync(order, 20, 100);
        await c.ReportAsync(order, 30, 100);
        await c.ReceiveMainAsync(order, 100);                                // the order completes by itself
        order.Status.Should().Be(OrderStatus.Completed);
        (await Refused(() => c.ReverseReportAsync(order, report.Id))).RuleName.Should().Be(MfgErrors.Op001);   // reopen the order first
    }

    [Fact]
    public async Task A_warehouse_outage_leaves_the_reversal_pending_and_the_job_finishes_it_once()
    {
        var (c, order) = await ReadyAsync();
        var vit = c.Items["NVL-VIT01"].Id;
        var report = (await c.ReportAsync(order, 10, 60)).Report;
        c.Stock.Intercept = (r, _) => r.MovementType == "ProductionReturn" ? MfgRules.Fail(MfgErrors.Int001) : null;

        var pending = await c.ReverseReportAsync(order, report.Id, key: "rev-1");

        pending.IsPending.Should().BeTrue();
        pending.Report.Status.Should().Be(OperationReportStatus.Pending);
        c.Op(order, 10).GoodQty.Should().Be(60m, "not applied until the Warehouse has the material back");
        c.Reports.Items.Single(r => r.Id == report.Id).IsReversed.Should().BeFalse();
        c.Reports.Items.Where(r => r.ReversalOfId != null && r.Status == OperationReportStatus.Pending).Should().ContainSingle("a pending reversal is not counted as a pending report");

        // while it waits, new reports, receipts and reversals of the order wait too
        (await Refused(() => c.ReportAsync(order, 10, 1))).RuleName.Should().Be(MfgErrors.Rev002);
        (await Refused(() => c.ReverseReportAsync(order, report.Id, key: "rev-2"))).RuleName.Should().BeOneOf(MfgErrors.Rev001, MfgErrors.Rev002);
        (await Refused(() => c.ReceiveMainAsync(order, 1))).RuleName.Should().Be(MfgErrors.Rev002);
        (await c.Reports.ListPendingForOrderAsync(order.Id)).Should().BeEmpty();

        c.Stock.Intercept = null;
        var document = c.Documents.Docs.Single(d => d.Type == StockDocumentType.Reversal);
        (await c.Posting.PostAsync(document, "job")).Status.Should().Be(StockDocumentStatus.Posted);
        (await c.Posting.PostAsync(document, "job")).Status.Should().Be(StockDocumentStatus.Posted);   // a second round changes nothing

        c.Op(order, 10).GoodQty.Should().Be(0m);
        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(0m);
        c.Stock.Total(vit).Should().Be(1600m);
        c.Stock.Movements.Count(m => m.Key == "RV-2611-00001#1").Should().Be(1, "exactly one movement per key");
        c.Reports.Items.Single(r => r.ReversalOfId == report.Id).Status.Should().Be(OperationReportStatus.Posted);
        c.Reports.Items.Single(r => r.Id == report.Id).IsReversed.Should().BeTrue();
        // and the same request again (same key) answers the same reversal
        (await c.ReverseReportAsync(order, report.Id, key: "rev-1")).Report.Id.Should().Be(pending.Report.Id);
    }

    [Fact]
    public async Task A_warehouse_refusal_fails_the_reversal_and_leaves_the_report_alone()
    {
        var (c, order) = await ReadyAsync();
        var report = (await c.ReportAsync(order, 10, 60)).Report;
        c.Stock.Intercept = (r, _) => r.MovementType == "ProductionReturn" ? new BusinessRuleException("WH_BROKEN", "refused by the Warehouse") : null;

        var ex = await Refused(() => c.ReverseReportAsync(order, report.Id));

        ex.RuleName.Should().Be("WH_BROKEN");
        c.Documents.Docs.Single(d => d.Type == StockDocumentType.Reversal).Status.Should().Be(StockDocumentStatus.Failed);
        c.Reports.Items.Single(r => r.ReversalOfId == report.Id).Status.Should().Be(OperationReportStatus.Failed);
        c.Reports.Items.Single(r => r.Id == report.Id).IsReversed.Should().BeFalse();
        c.Op(order, 10).GoodQty.Should().Be(60m);

        c.Stock.Intercept = null;                                            // a failed reversal does not block the next try
        (await c.ReverseReportAsync(order, report.Id)).Report.Status.Should().Be(OperationReportStatus.Posted);
        c.Op(order, 10).GoodQty.Should().Be(0m);
    }

    [Fact]
    public async Task The_same_key_is_the_same_reversal_and_another_reason_under_it_is_a_conflict()
    {
        var (c, order) = await ReadyAsync();
        var report = (await c.ReportAsync(order, 10, 60)).Report;
        var first = await c.ReverseReportAsync(order, report.Id, "wrong count", "k-1");

        var again = await c.ReverseReportAsync(order, report.Id, "wrong count", "k-1");

        again.Report.Id.Should().Be(first.Report.Id);
        c.Documents.Docs.Count(d => d.Type == StockDocumentType.Reversal).Should().Be(1);
        (await Refused(() => c.ReverseReportAsync(order, report.Id, "another reason", "k-1"))).RuleName.Should().Be(MfgErrors.Gen007);
    }

    // ── Receipt (BR-FG-07) ─────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-REV-004, TC-BF-007 (the backflush part of a receipt comes back)
    public async Task Reversing_a_receipt_scraps_its_lot_returns_its_backflush_and_lowers_the_completed_quantity()
    {
        var (c, order) = await ReadyAsync(vitUnbound: true);
        await c.ProduceAsync(order, 40);
        var vit = c.Items["NVL-VIT01"].Id;
        var tp = c.Items["TP-BAN01"].Id;
        var receipt = (await c.ReceiveMainAsync(order, 40)).Document;
        var vitBefore = c.Stock.Total(vit);
        var consumed = c.Mat(order, "NVL-VIT01").ConsumedQty;
        consumed.Should().BeGreaterThan(0m);
        var lot = c.Stock.Balances[tp].Single();
        lot.Quantity.Should().Be(40m);

        var result = await c.ReverseReceiptAsync(order, receipt.Id);

        result.IsPending.Should().BeFalse();
        result.Document.Should().Match<StockDocumentDto>(d => d.Type == StockDocumentType.Reversal && d.Status == StockDocumentStatus.Posted && d.ReversalOfId == receipt.Id);
        result.Document.Lines.Select(l => (l.Kind, l.ReversalAction)).Should().Equal((StockLineKind.Receipt, ReversalAction.Scrap), (StockLineKind.Material, ReversalAction.Return));
        order.CompletedQty.Should().Be(0m);
        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(0m);
        c.Stock.Total(vit).Should().Be(vitBefore + consumed, "the backflush part of the receipt is back");
        c.Stock.Balances[tp].Single().Quantity.Should().Be(0m, "the lot the receipt created is gone");
        c.Documents.Docs.Single(d => d.Id == receipt.Id).IsReversed.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact] // TC-REV-005
    public async Task A_receipt_whose_lot_was_released_from_quality_hold_cannot_be_reversed()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 40);
        var receipt = (await c.ReceiveMainAsync(order, 40)).Document;
        await c.QcReleaseAsync(receipt.Id);
        var calls = c.Stock.MovementCalls;

        var ex = await Refused(() => c.ReverseReceiptAsync(order, receipt.Id));

        ex.RuleName.Should().Be(MfgErrors.Fg004);
        ex.Message.Should().Contain("giải phóng");
        c.Stock.MovementCalls.Should().Be(calls);
        c.Documents.Docs.Should().NotContain(d => d.Type == StockDocumentType.Reversal);
    }

    [Fact] // TC-REV-006
    public async Task A_receipt_whose_lot_was_partly_used_cannot_be_reversed()
    {
        var (c, order) = await ReadyAsync(fqc: false);
        await c.ProduceAsync(order, 40);
        var receipt = (await c.ReceiveMainAsync(order, 40)).Document;
        var tp = c.Items["TP-BAN01"].Id;
        var lotId = c.Stock.Balances[tp].Single().LotId;
        c.Stock.Adjust(tp, lotId, -10m);                                      // 10 of the 40 were delivered

        var ex = await Refused(() => c.ReverseReceiptAsync(order, receipt.Id));

        ex.RuleName.Should().Be(MfgErrors.Fg004);
        ex.Message.Should().Contain("30");
        c.Documents.Docs.Should().NotContain(d => d.Type == StockDocumentType.Reversal);
        order.CompletedQty.Should().Be(40m);
    }

    [Fact] // TC-REV-007
    public async Task Reversing_a_receipt_of_a_completed_order_reopens_it_when_it_falls_below_the_threshold()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        var first = (await c.ReceiveMainAsync(order, 40)).Document;
        await c.ReceiveMainAsync(order, 60);
        order.Status.Should().Be(OrderStatus.Completed);

        await c.ReverseReceiptAsync(order, first.Id);

        order.CompletedQty.Should().Be(60m);
        order.Status.Should().Be(OrderStatus.InProgress);
        order.ActualEnd.Should().BeNull();
        order.History.Last().Note.Should().Contain("receipt reversed");
        order.ReservationSyncPending.Should().BeFalse("the reservation was brought in line after the save");
    }

    [Fact]
    public async Task A_completed_order_that_still_meets_the_tolerance_stays_completed()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        await c.ReceiveMainAsync(order, 99);
        var last = (await c.ReceiveMainAsync(order, 1)).Document;
        order.Status.Should().Be(OrderStatus.Completed);

        await c.ReverseReceiptAsync(order, last.Id);

        order.CompletedQty.Should().Be(99m);
        order.Status.Should().Be(OrderStatus.Completed, "99 of 100 meets the 2 % under-completion tolerance");
    }

    [Fact]
    public async Task A_co_product_receipt_is_reversed_with_its_output_line()
    {
        var c = new OpsCtx();
        await c.SeedAllAsync();
        c.StockDs01();
        var draft = await c.CreateOrderAsync("BTP-MAT01", 50);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        var receipt = (await c.ReceiveAsync(order, [new ReceiptLineBody(null, 48, null), new ReceiptLineBody(order.Outputs.Single().LineNo, 24, null)])).Document;
        order.Outputs.Single().ReceivedQty.Should().Be(24m);

        await c.ReverseReceiptAsync(order, receipt.Id);

        (order.CompletedQty, order.Outputs.Single().ReceivedQty).Should().Be((0m, 0m));
        c.Stock.Total(c.Items["PP-MUN01"].Id).Should().Be(c.Stock.Total(c.Items["PP-MUN01"].Id), "the by-product left stock again");
        c.Stock.Total(c.Items["BTP-MAT01"].Id).Should().Be(100m, "only the opening stock is left");
    }

    [Fact]
    public async Task A_reversed_receipt_no_longer_counts_as_a_held_lot_and_its_quantity_can_be_received_again()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        var receipt = (await c.ReceiveMainAsync(order, 40)).Document;
        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeFalse();

        await c.ReverseReceiptAsync(order, receipt.Id);

        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeTrue();
        (await c.ReceiveMainAsync(order, 100)).Document.Status.Should().Be(StockDocumentStatus.Posted);
        order.CompletedQty.Should().Be(100m);
    }

    [Fact]
    public async Task A_receipt_is_reversed_once_and_only_a_posted_receipt_can_be_reversed()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        var receipt = (await c.ReceiveMainAsync(order, 40)).Document;
        await c.ReverseReceiptAsync(order, receipt.Id);
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1)]);
        var issue = c.Documents.Docs.Last(d => d.Type == StockDocumentType.Issue);

        (await Refused(() => c.ReverseReceiptAsync(order, receipt.Id))).RuleName.Should().Be(MfgErrors.Rev001);
        (await Refused(() => c.ReverseReceiptAsync(order, issue.Id))).RuleName.Should().Be(MfgErrors.Rev001);
        (await Refused(() => c.ReverseReceiptAsync(order, c.Documents.Docs.Single(d => d.Type == StockDocumentType.Reversal).Id))).RuleName.Should().Be(MfgErrors.Rev001);
        await FluentActions.Awaiting(() => c.ReverseReceiptAsync(order, Guid.NewGuid())).Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task A_receipt_needs_a_reason_an_open_order_and_a_reachable_warehouse()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        var receipt = (await c.ReceiveMainAsync(order, 40)).Document;

        (await Refused(() => c.ReverseReceiptAsync(order, receipt.Id, note: null))).RuleName.Should().Be("MFG_VALIDATION");
        c.Stock.Down = true;
        (await Refused(() => c.ReverseReceiptAsync(order, receipt.Id))).RuleName.Should().Be(MfgErrors.Int001);   // the lot cannot be checked: nothing is stored
        c.Documents.Docs.Should().NotContain(d => d.Type == StockDocumentType.Reversal);
        c.Stock.Down = false;
        await c.HoldAsync(order.Id, c.HoldReason.Id);
        (await Refused(() => c.ReverseReceiptAsync(order, receipt.Id))).RuleName.Should().Be(MfgErrors.Po011);
    }

    [Fact]
    public async Task A_pending_receipt_reversal_is_finished_once_and_blocks_new_receipts()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        var receipt = (await c.ReceiveMainAsync(order, 40)).Document;
        c.Stock.Intercept = (r, _) => r.MovementType == "ProductionScrap" ? MfgRules.Fail(MfgErrors.Int001) : null;

        var pending = await c.ReverseReceiptAsync(order, receipt.Id, key: "r-1");

        pending.IsPending.Should().BeTrue();
        order.CompletedQty.Should().Be(40m);
        (await Refused(() => c.ReceiveMainAsync(order, 10))).RuleName.Should().Be(MfgErrors.Rev002);
        (await Refused(() => c.CompleteOrderAsync(order))).RuleName.Should().Be(MfgErrors.Po013);       // a pending document blocks completion

        c.Stock.Intercept = null;
        var document = c.Documents.Docs.Single(d => d.Type == StockDocumentType.Reversal);
        (await c.Posting.PostAsync(document, "job")).Status.Should().Be(StockDocumentStatus.Posted);
        order.CompletedQty.Should().Be(0m);
        c.Stock.Movements.Count(m => m.Key == document.Lines[0].MovementKey).Should().Be(1);
        (await c.ReverseReceiptAsync(order, receipt.Id, key: "r-1")).Document.Id.Should().Be(pending.Document.Id);
    }

    // ── Engine ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_posted_line_keeps_the_lots_the_warehouse_used_and_a_reversal_gives_each_lot_its_quantity_back()
    {
        var (c, order) = await ReadyAsync();
        var son = c.Items["NVL-SON01"];
        var lotA = Guid.NewGuid();
        var lotB = Guid.NewGuid();
        var material = c.Mat(order, "NVL-SON01");
        var original = MaterialTransaction.Create("BF-2611-00099", StockDocumentType.Backflush, order.PlantId, order.Id, order.OrderNo, new DateOnly(2026, 11, 4), null, null, null, null,
            [new NewStockLine(material.Id, material.LineNo, son.Id, "NVL-SON01", c.RawWarehouse, null, 52.5m, true, null)], "t");
        original.RecordLinePosted(original.Lines.Single(), Guid.NewGuid(), 4m, 210m, null,
            [new LineLot(lotA, "L-SON-A", 30m, 4m, c.RawWarehouse, null), new LineLot(lotB, "L-SON-B", 22.5m, 4m, c.RawWarehouse, null)]);
        original.MarkPosted("t", DateTime.UtcNow);
        original.Lines.Single().Lots.Select(l => l.LotNumber).Should().Equal("L-SON-A", "L-SON-B");
        c.Documents.Add(original);
        var reversal = MaterialTransaction.Create("RV-2611-00099", StockDocumentType.Reversal, order.PlantId, order.Id, order.OrderNo, new DateOnly(2026, 11, 4), null, "test", null, null,
            ReversalPlanner.ReturnMaterial(original), "t", null, original.Id);
        c.Documents.Add(reversal);

        var result = await c.Posting.PostAsync(reversal, "t");

        result.Status.Should().Be(StockDocumentStatus.Posted);
        c.Stock.Requests.Where(r => r.MovementType == "ProductionReturn").Select(r => (r.LotId, r.Quantity, r.IdempotencyKey)).Should().Equal(
            ((Guid?)lotA, 30m, "RV-2611-00099#1"), (lotB, 22.5m, "RV-2611-00099#2"));
        original.IsReversed.Should().BeTrue();
    }

    [Fact]
    public async Task A_refusal_in_the_middle_of_a_reversal_undoes_the_lines_already_posted()
    {
        var (c, order) = await ReadyAsync();
        var original = MaterialTransaction.Create("BF-2611-00098", StockDocumentType.Backflush, order.PlantId, order.Id, order.OrderNo, new DateOnly(2026, 11, 4), null, null, null, null,
            [new NewStockLine(c.Mat(order, "NVL-VIT01").Id, 1, c.Items["NVL-VIT01"].Id, "NVL-VIT01", c.RawWarehouse, null, 10m, false, null),
             new NewStockLine(c.Mat(order, "NVL-KEO01").Id, 2, c.Items["NVL-KEO01"].Id, "NVL-KEO01", c.RawWarehouse, null, 5m, false, null)], "t");
        foreach (var line in original.Lines) original.RecordLinePosted(line, Guid.NewGuid(), 2m, line.Qty * 2m, null);
        original.MarkPosted("t", DateTime.UtcNow);
        c.Documents.Add(original);
        var reversal = MaterialTransaction.Create("RV-2611-00098", StockDocumentType.Reversal, order.PlantId, order.Id, order.OrderNo, new DateOnly(2026, 11, 4), null, "test", null, null,
            ReversalPlanner.ReturnMaterial(original), "t", null, original.Id);
        c.Documents.Add(reversal);
        c.Stock.Intercept = (r, _) => r.MovementType == "ProductionReturn" && r.IdempotencyKey.EndsWith("#2") ? new BusinessRuleException("WH_BROKEN", "refused") : null;
        var vitBefore = c.Stock.Total(c.Items["NVL-VIT01"].Id);

        var result = await c.Posting.PostAsync(reversal, "t");

        result.Status.Should().Be(StockDocumentStatus.Failed);
        reversal.Lines.Select(l => l.Status).Should().Equal(StockLineStatus.Compensated, StockLineStatus.Refused);
        c.Stock.Movements.Should().Contain(m => m.Key == "RV-2611-00098#1R" && m.Type == "ProductionIssue", "the screws that came back leave again");
        c.Stock.Total(c.Items["NVL-VIT01"].Id).Should().Be(vitBefore);
        original.IsReversed.Should().BeFalse();
    }
}
