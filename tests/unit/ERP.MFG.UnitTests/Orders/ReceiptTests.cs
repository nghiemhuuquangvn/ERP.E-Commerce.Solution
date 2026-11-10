using ERP.MFG.Application.Features.Receipts;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

public class ReceiptRulesTests
{
    [Theory]
    [InlineData("{ItemCode}-{yyMMdd}-{seq2}", 1, "TP-BAN01-261104-01")]
    [InlineData("{ItemCode}-{yyMMdd}-{seq2}", 12, "TP-BAN01-261104-12")]
    [InlineData("{ItemCode}-{yyyyMMdd}-{seq3}", 3, "TP-BAN01-20261104-003")]
    [InlineData("LOT-{yyMMdd}", 1, "LOT-261104")]
    [InlineData("LOT-{yyMMdd}", 2, "LOT-261104-02")]      // a format without a sequence only needs one after the first
    public void The_lot_number_follows_the_plants_format(string format, int sequence, string expected)
        => LotNumberFormatter.Format(format, "TP-BAN01", new DateOnly(2026, 11, 4), sequence).Should().Be(expected);

    [Fact]
    public void The_next_lot_number_is_the_smallest_free_one_of_the_day()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TP-BAN01-261104-01", "tp-ban01-261104-02" };

        LotNumberFormatter.Next("{ItemCode}-{yyMMdd}-{seq2}", "TP-BAN01", new DateOnly(2026, 11, 4), taken).Should().Be("TP-BAN01-261104-03");
    }

    [Theory]
    [InlineData(100, 5, null, 105)]      // no routing: the tolerance ceiling (TC-FG-009 is 200 -> 210)
    [InlineData(100, 5, 50, 50)]         // TC-FG-002: the last operation gave 50 good pieces
    [InlineData(100, 5, 110, 105)]       // TC-FG-003: bad data (110 good) cannot lift the tolerance ceiling
    [InlineData(200, 5, null, 210)]
    public void The_main_ceiling_is_the_smaller_of_the_tolerance_and_the_last_operations_good_output(decimal qty, decimal tol, int? lastGood, decimal expected)
        => ReceiptLimits.MainCeiling(qty, tol, lastGood is null ? null : (decimal)lastGood.Value).Should().Be(expected);

    [Fact]
    public void An_output_may_exceed_its_plan_by_the_tolerance_only()
    {
        ReceiptLimits.OutputCeiling(25m, 5m).Should().Be(26.25m);     // TC-FG-011
        ReceiptLimits.Remaining(105m, 100m, 3m).Should().Be(2m);
        ReceiptLimits.Remaining(105m, 105m, 3m).Should().Be(0m);
    }

    [Theory]
    [InlineData(98, 100, 2, true)]     // TC-PO-030
    [InlineData(97, 100, 2, false)]    // TC-PO-031
    [InlineData(100, 100, 0, true)]
    public void Completion_without_a_short_close_needs_the_under_completion_threshold(decimal completed, decimal qty, decimal tol, bool ok)
        => OrderCompletion.MeetsThreshold(completed, qty, tol).Should().Be(ok);
}

public class ReceiptTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> act) => (await act.Should().ThrowAsync<BusinessRuleException>()).Which;

    /// <summary>MO-A of 100 pieces, released, with the finished good lot-controlled and needing final inspection.</summary>
    private static async Task<(OpsCtx C, ProductionOrder Order)> ReadyAsync(bool lot = true, bool fqc = true, int shelf = 0, bool sonBackflush = false, bool vitUnbound = false)
    {
        var c = new OpsCtx { SonBackflush = sonBackflush, VitUnbound = vitUnbound };
        await c.SeedAllAsync();
        c.StockDs01();
        c.FinishedGoodIs(lot, fqc, shelf);
        var draft = await c.CreateOrderAsync("TP-BAN01", 100);
        await c.ReleaseAsync(draft.Id);
        return (c, c.Stored(draft.Id));
    }

    // ── Receipts ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-FG-001, TC-PO-029
    public async Task Receiving_all_100_puts_the_lot_on_hold_in_the_quarantine_warehouse_and_completes_the_order()
    {
        var (c, order) = await ReadyAsync(shelf: 180);
        await c.ProduceAsync(order, 100);

        var result = await c.ReceiveMainAsync(order, 100);

        result.Document.Status.Should().Be(StockDocumentStatus.Posted);
        result.Document.DocNo.Should().Be("FR-2611-00001");
        var line = result.Document.Lines.Single(l => l.Kind == StockLineKind.Receipt);
        (line.LotNumber, line.IsQcHold, line.WarehouseId, line.Qty).Should().Be(("TP-BAN01-261104-01", true, c.Plant.QuarantineWarehouseId, 100m));
        line.ExpiryDate!.Value.Date.Should().Be(new DateTime(2027, 5, 3));                 // TC-FG-007: 2026-11-04 + 180 days
        line.ManufactureDate!.Value.Date.Should().Be(new DateTime(2026, 11, 4));           // P3-6: the Receipts tab shows the manufacture date too
        var stored = c.Stock.Balances[c.Items["TP-BAN01"].Id].Single();
        (stored.LotNumber, stored.LotStatus, stored.WarehouseId).Should().Be(("TP-BAN01-261104-01", "QcHold", c.Plant.QuarantineWarehouseId));
        order.CompletedQty.Should().Be(100m);
        order.Status.Should().Be(OrderStatus.Completed);
        order.ActualEnd.Should().NotBeNull();
        order.Operations.Should().OnlyContain(o => o.Status == OperationStatus.Completed);
        c.Stock.HeldFor(order.Id, c.Items["NVL-KEO01"].Id).Should().Be(0m, "the reservation goes back to the Warehouse when the order completes");
    }

    [Fact] // TC-FG-002
    public async Task The_last_operations_good_output_is_the_ceiling()
    {
        var (c, order) = await ReadyAsync();
        await c.ReportAsync(order, 10, 100);
        await c.ReportAsync(order, 20, 100);
        await c.ReportAsync(order, 30, 50);

        var ex = await Refused(() => c.ReceiveMainAsync(order, 51));

        ex.RuleName.Should().Be(MfgErrors.Fg001);
        ex.Message.Should().Contain("50");
        c.Documents.Docs.Where(d => d.Type == StockDocumentType.Receipt).Should().BeEmpty();
        (await c.ReceiveMainAsync(order, 50)).Document.Status.Should().Be(StockDocumentStatus.Posted);
    }

    [Fact] // TC-FG-004
    public async Task The_tolerance_boundary_105_is_accepted_and_one_more_is_not()
    {
        var (c, order) = await ReadyAsync();
        c.PlantSettings.AutoCompleteOnFullReceipt = false;
        c.Stock.Stock(c.Items["NVL-VIT01"].Id, c.RawWarehouse, 200);
        c.Stock.Stock(c.Items["NVL-THUNG01"].Id, c.RawWarehouse, 10);
        c.Stock.Stock(c.Items["NVL-XOP01"].Id, c.RawWarehouse, 20);
        await c.ProduceAsync(order, 105);

        (await c.ReceiveMainAsync(order, 105)).Document.Status.Should().Be(StockDocumentStatus.Posted);
        (await Refused(() => c.ReceiveMainAsync(order, 1))).RuleName.Should().Be(MfgErrors.Fg001);
        order.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact] // TC-FG-005
    public async Task Two_receipts_on_one_day_get_lots_01_and_02()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);

        var first = await c.ReceiveMainAsync(order, 40);
        var second = await c.ReceiveMainAsync(order, 60);

        first.Document.Lines.Single().LotNumber.Should().Be("TP-BAN01-261104-01");
        second.Document.Lines.Single().LotNumber.Should().Be("TP-BAN01-261104-02");
    }

    [Fact] // TC-FG-006
    public async Task A_manual_lot_number_that_exists_is_refused_even_when_the_lot_is_used_up()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        c.Stock.DepletedLotNumbers[c.Items["TP-BAN01"].Id] = ["L-TP-OLD"];

        (await Refused(() => c.ReceiveMainAsync(order, 10, "l-tp-old"))).RuleName.Should().Be(MfgErrors.Fg002);

        var ok = await c.ReceiveMainAsync(order, 10, "L-TP-NEW");
        ok.Document.Lines.Single().LotNumber.Should().Be("L-TP-NEW");
        (await Refused(() => c.ReceiveMainAsync(order, 10, "L-TP-NEW"))).RuleName.Should().Be(MfgErrors.Fg002);   // and now it exists here
    }

    [Fact] // TC-FG-008
    public async Task A_finished_good_without_final_inspection_goes_straight_to_the_finished_goods_warehouse()
    {
        var (c, order) = await ReadyAsync(lot: false, fqc: false);
        await c.ProduceAsync(order, 100);

        var result = await c.ReceiveMainAsync(order, 48);

        var line = result.Document.Lines.Single(l => l.Kind == StockLineKind.Receipt);
        (line.LotNumber, line.IsQcHold, line.WarehouseId).Should().Be((null, false, order.OutputWarehouseId));
        var row = c.Stock.Balances[c.Items["TP-BAN01"].Id].Single();
        (row.LotId, row.WarehouseId, row.AvailableQuantity).Should().Be((null, order.OutputWarehouseId, 48m));
    }

    [Fact] // TC-FG-009
    public async Task An_order_without_a_routing_may_receive_the_tolerance_ceiling_210()
    {
        var c = new OpsCtx();
        await c.SeedAllAsync();
        c.StockDs01();
        var draft = await c.CreateOrderAsync("BTP-CHAN01", 200);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);

        (await Refused(() => c.ReceiveMainAsync(order, 211))).RuleName.Should().Be(MfgErrors.Fg001);
        (await c.ReceiveMainAsync(order, 210)).Document.Status.Should().Be(StockDocumentStatus.Posted);
        order.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact] // TC-FG-010 / 011
    public async Task A_by_product_is_received_with_the_main_product_and_has_its_own_ceiling()
    {
        var c = new OpsCtx();
        await c.SeedAllAsync();
        c.StockDs01();
        var draft = await c.CreateOrderAsync("BTP-MAT01", 50);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        var output = order.Outputs.Single();
        output.PlannedQty.Should().Be(25m);

        (await Refused(() => c.ReceiveAsync(order, [new ReceiptLineBody(output.LineNo, 26.26m, null)]))).RuleName.Should().Be(MfgErrors.Fg001);
        var result = await c.ReceiveAsync(order, [new ReceiptLineBody(null, 48, null), new ReceiptLineBody(output.LineNo, 24, null)]);

        result.Document.Lines.Count(l => l.Kind == StockLineKind.Receipt).Should().Be(2);
        (order.CompletedQty, output.ReceivedQty).Should().Be((48m, 24m));
    }

    [Fact] // TC-FG-012
    public async Task A_serial_controlled_finished_good_is_refused()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        c.Replace("TP-BAN01", p => p with { IsSerialControlled = true });

        (await Refused(() => c.ReceiveMainAsync(order, 10))).RuleName.Should().Be(MfgErrors.Mat011);
    }

    [Fact] // TC-FG-013
    public async Task Without_auto_complete_the_order_stays_in_progress()
    {
        var (c, order) = await ReadyAsync();
        c.PlantSettings.AutoCompleteOnFullReceipt = false;
        await c.ProduceAsync(order, 100);

        await c.ReceiveMainAsync(order, 100);

        order.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact]
    public async Task A_receipt_needs_an_open_order_and_quantities_within_the_units_decimals()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);

        (await Refused(() => c.ReceiveMainAsync(order, 0))).RuleName.Should().Be(MfgErrors.Mat001);
        await Refused(() => c.ReceiveMainAsync(order, 1.5m));        // pieces have no decimals
        await c.HoldAsync(order.Id, c.HoldReason.Id);
        (await Refused(() => c.ReceiveMainAsync(order, 10))).RuleName.Should().Be(MfgErrors.Po011);
        await c.ResumeAsync(order.Id);

        var draftCtx = new OpsCtx();
        var draft = await draftCtx.DraftAsync();
        (await Refused(() => draftCtx.ReceiveMainAsync(draftCtx.Stored(draft.Id), 1))).RuleName.Should().Be(MfgErrors.Fg005);
    }

    [Fact]
    public async Task The_same_idempotency_key_returns_the_same_receipt()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);

        var first = await c.ReceiveMainAsync(order, 40, key: "fr-1");
        var again = await c.ReceiveMainAsync(order, 40, key: "fr-1");

        again.Document.Id.Should().Be(first.Document.Id);
        order.CompletedQty.Should().Be(40m);
        (await Refused(() => c.ReceiveMainAsync(order, 41, key: "fr-1"))).RuleName.Should().Be(MfgErrors.Gen007);
    }

    // ── Backflush on receipt (TC-BF-007) ───────────────────────────────────────────────────────────────────

    [Fact] // TC-BF-007
    public async Task Screws_that_belong_to_no_operation_are_consumed_when_finished_goods_are_received()
    {
        var (c, order) = await ReadyAsync(vitUnbound: true);
        c.Mat(order, "NVL-VIT01").OperationSeq.Should().BeNull();
        await c.ProduceAsync(order, 20);
        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(0m, "the operations do not own the screws");
        var vit = c.Items["NVL-VIT01"].Id;

        var result = await c.ReceiveMainAsync(order, 10);

        result.Document.Lines.Select(l => l.Kind).Should().Equal(StockLineKind.Material, StockLineKind.Receipt);   // consumption first
        result.Document.Lines.First().Qty.Should().Be(160m);
        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(160m);
        c.Stock.Total(vit).Should().Be(1600m - 160m);
        await c.ReceiveMainAsync(order, 10);
        c.Mat(order, "NVL-VIT01").ConsumedQty.Should().Be(320m);
    }

    [Fact]
    public async Task A_shortage_of_unbound_backflush_material_refuses_the_whole_receipt()
    {
        var (c, order) = await ReadyAsync(vitUnbound: true);
        await c.ProduceAsync(order, 10);
        var vit = c.Items["NVL-VIT01"].Id;
        c.Stock.Balances.Remove(vit);
        c.Stock.Stock(vit, c.RawWarehouse, 100);                     // 160 are needed
        var held = c.Stock.Reservations[order.Id];
        c.Stock.Reservations[order.Id] = held with { Lines = held.Lines.Where(l => l.ProductId != vit).ToList() };   // and the order holds none of them
        var calls = c.Stock.MovementCalls;

        var ex = await Refused(() => c.ReceiveMainAsync(order, 10));

        ex.RuleName.Should().Be(MfgErrors.Mat008);
        order.CompletedQty.Should().Be(0m);
        c.Documents.Docs.Where(d => d.Type == StockDocumentType.Receipt).Should().BeEmpty();
        c.Stock.MovementCalls.Should().Be(calls);
    }

    // ── Engine: outage, refusal, reversal ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_warehouse_outage_leaves_the_receipt_pending_and_the_job_posts_it_once()
    {
        var (c, order) = await ReadyAsync(lot: false, fqc: false);
        await c.ProduceAsync(order, 100);
        c.Stock.Intercept = (r, _) => r.MovementType == "ProductionReceipt" ? MfgRules.Fail(MfgErrors.Int001) : null;

        var result = await c.ReceiveMainAsync(order, 100);

        result.IsPending.Should().BeTrue();
        order.CompletedQty.Should().Be(0m);
        order.Status.Should().Be(OrderStatus.InProgress);
        (await Refused(() => c.CompleteOrderAsync(order))).RuleName.Should().Be(MfgErrors.Po013);     // a pending document blocks completion
        var document = c.Documents.Docs.Single(d => d.Type == StockDocumentType.Receipt);

        c.Stock.Intercept = null;
        await c.Posting.PostAsync(document, "job");
        await c.Posting.PostAsync(document, "job");

        document.Status.Should().Be(StockDocumentStatus.Posted);
        c.Stock.Movements.Count(m => m.Type == "ProductionReceipt").Should().Be(1);
        order.CompletedQty.Should().Be(100m);
        order.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task A_refused_receipt_line_reverses_the_consumption_and_fails_the_document()
    {
        var (c, order) = await ReadyAsync(lot: false, fqc: false, vitUnbound: true);
        await c.ProduceAsync(order, 10);
        c.Stock.Intercept = (r, _) => r.MovementType == "ProductionReceipt" ? new BusinessRuleException("WH_BROKEN", "refused by the Warehouse") : null;
        var vit = c.Items["NVL-VIT01"].Id;

        var ex = await Refused(() => c.ReceiveMainAsync(order, 10));

        ex.RuleName.Should().Be("WH_BROKEN");
        c.Stock.Total(vit).Should().Be(1600m, "the consumption line was booked and reversed");
        var document = c.Documents.Docs.Single(d => d.Type == StockDocumentType.Receipt);
        document.Status.Should().Be(StockDocumentStatus.Failed);
        document.Lines.Select(l => l.Status).Should().Equal(StockLineStatus.Compensated, StockLineStatus.Refused);
        (order.CompletedQty, c.Mat(order, "NVL-VIT01").ConsumedQty).Should().Be((0m, 0m));
    }

    [Fact]
    public async Task A_refused_second_receipt_line_scraps_the_lot_the_first_one_created()
    {
        var c = new OpsCtx();
        await c.SeedAllAsync();
        c.StockDs01();
        var draft = await c.CreateOrderAsync("BTP-MAT01", 50);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        var byProduct = c.Items["PP-MUN01"].Id;
        c.Stock.Intercept = (r, _) => r.MovementType == "ProductionReceipt" && r.ProductId == byProduct ? new BusinessRuleException("WH_BROKEN", "no room") : null;

        await Refused(() => c.ReceiveAsync(order, [new ReceiptLineBody(null, 48, null), new ReceiptLineBody(order.Outputs.Single().LineNo, 24, null)]));

        c.Stock.Total(c.Items["BTP-MAT01"].Id).Should().Be(100m, "the main product line was booked and reversed: only the opening stock is left");
        c.Stock.Movements.Should().Contain(m => m.Type == "ProductionScrap" || (m.Type == "ProductionIssue" && m.Key.EndsWith("R")));
        order.CompletedQty.Should().Be(0m);
    }

    // ── Interim quality release ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_interim_quality_release_moves_the_lot_to_the_finished_goods_warehouse_and_makes_it_available()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        var document = (await c.ReceiveMainAsync(order, 100)).Document;
        var item = c.Items["TP-BAN01"].Id;

        var released = await c.QcReleaseAsync(document.Id);

        var row = c.Stock.Balances[item].Single();
        (row.LotStatus, row.WarehouseId).Should().Be(("Available", order.OutputWarehouseId));
        released.Lines.Single(l => l.Kind == StockLineKind.Receipt).QcReleasedAt.Should().NotBeNull();
        c.Stock.Movements.Select(m => m.Key).Should().Contain([document.Lines.Single(l => l.Kind == StockLineKind.Receipt).MovementKey + "Q1", document.Lines.Single(l => l.Kind == StockLineKind.Receipt).MovementKey + "Q2"]);
        var calls = c.Stock.MovementCalls;
        await c.QcReleaseAsync(document.Id);                                  // nothing is left to release
        c.Stock.MovementCalls.Should().Be(calls);
    }

    [Fact]
    public async Task A_half_done_quality_release_is_finished_by_calling_again()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        var document = (await c.ReceiveMainAsync(order, 100)).Document;
        var key = document.Lines.Single(l => l.Kind == StockLineKind.Receipt).MovementKey;
        c.Stock.Intercept = (r, _) => r.MovementType == "QcRelease" ? MfgRules.Fail(MfgErrors.Int001) : null;

        (await Refused(() => c.QcReleaseAsync(document.Id))).RuleName.Should().Be(MfgErrors.Int001);
        c.Stock.Balances[c.Items["TP-BAN01"].Id].Single().WarehouseId.Should().Be(order.OutputWarehouseId, "the transfer went through");

        c.Stock.Intercept = null;
        await c.QcReleaseAsync(document.Id);

        c.Stock.Balances[c.Items["TP-BAN01"].Id].Single().LotStatus.Should().Be("Available");
        c.Stock.Movements.Count(m => m.Key == key + "Q1").Should().Be(1);
    }

    [Fact]
    public async Task Only_a_posted_receipt_can_be_released_from_quality_hold()
    {
        var (c, order) = await ReadyAsync();
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1)]);

        await Refused(() => c.QcReleaseAsync(c.Documents.Docs.Single().Id));
    }

    // ── Complete / short-close / reopen ────────────────────────────────────────────────────────────────────

    [Fact] // TC-PO-030
    public async Task Within_the_under_completion_tolerance_the_order_completes_without_a_reason()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 98);
        await c.ReceiveMainAsync(order, 98);
        order.Status.Should().Be(OrderStatus.InProgress);

        await c.CompleteOrderAsync(order);

        order.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact] // TC-PO-031 / 032
    public async Task Below_the_threshold_only_a_short_close_with_permission_and_reason_completes()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 97);
        await c.ReceiveMainAsync(order, 97);

        (await Refused(() => c.CompleteOrderAsync(order))).RuleName.Should().Be(MfgErrors.Po012);
        (await Refused(() => c.CompleteOrderAsync(order, c.ShortCloseReason.Id))).RuleName.Should().Be(MfgErrors.Po012);        // reason, no permission
        (await Refused(() => c.CompleteOrderAsync(order, c.HoldReason.Id, PermissionConstants.Manufacturing_OrderShortClose))).RuleName.Should().Be(MfgErrors.Po012);   // permission, wrong reason

        await c.CompleteOrderAsync(order, c.ShortCloseReason.Id, PermissionConstants.Manufacturing_OrderShortClose);

        order.Status.Should().Be(OrderStatus.Completed);
        order.History.Last().ReasonCodeId.Should().Be(c.ShortCloseReason.Id);
    }

    [Fact] // TC-PO-033
    public async Task An_order_with_a_running_operation_cannot_be_completed()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 98);
        await c.ReceiveMainAsync(order, 98);
        await c.StartAsync(order, 30);

        (await Refused(() => c.CompleteOrderAsync(order))).RuleName.Should().Be(MfgErrors.Po013);
    }

    [Fact] // TC-PO-034
    public async Task A_completed_order_can_be_reopened_and_gets_its_reservation_back()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        await c.ReceiveMainAsync(order, 100);
        var keo = c.Items["NVL-KEO01"].Id;
        c.Stock.HeldFor(order.Id, keo).Should().Be(0m);

        await c.ReopenOrderAsync(order);

        order.Status.Should().Be(OrderStatus.InProgress);
        order.ActualEnd.Should().BeNull();
        c.Stock.HeldFor(order.Id, keo).Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task Completing_closes_the_open_time_logs()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 97);
        await c.StartAsync(order, 30);
        await c.ReceiveMainAsync(order, 97);
        await c.PauseLogAsync(c.TimeLogs.Items.Single().Id);

        await c.CompleteOrderAsync(order, c.ShortCloseReason.Id, PermissionConstants.Manufacturing_OrderShortClose);

        c.TimeLogs.Items.Single().IsOpen.Should().BeFalse();
        c.Op(order, 30).Status.Should().Be(OperationStatus.Completed);
    }

    // ── Close ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-PO-036 / 037
    public async Task Close_lists_the_conditions_that_are_not_met()
    {
        var (c, order) = await ReadyAsync();
        await c.IssueAsync(order, [c.Line(order, "NVL-SON01", 52.5m)]);
        await c.ProduceAsync(order, 100);
        await c.ReceiveMainAsync(order, 100);                      // completes; the lot waits for quality

        var checklist = await c.ChecklistAsync(order);
        var ex = await Refused(() => c.CloseOrderAsync(order));

        checklist.CanClose.Should().BeFalse();
        checklist.Conditions.Where(x => !x.Met).Select(x => x.Code).Should().BeEquivalentTo("QUALITY_HOLD", "UNSETTLED_MATERIAL");
        ex.RuleName.Should().Be(MfgErrors.Po013);
        ex.Message.Should().Contain("NVL-SON01").And.Contain("52.5");
        order.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact] // TC-PO-038, TC-PO-039, TC-PO-042
    public async Task Closing_with_confirmation_carries_the_rest_to_consumption_and_makes_the_order_read_only()
    {
        var (c, order) = await ReadyAsync();
        await c.IssueAsync(order, [c.Line(order, "NVL-SON01", 52.5m)]);
        await c.ProduceAsync(order, 100);
        var receipt = (await c.ReceiveMainAsync(order, 100)).Document;
        await c.QcReleaseAsync(receipt.Id);
        await c.ReturnAsync(order, [c.Line(order, "NVL-SON01", 1m)]);       // unused paint goes back before the close (allowed on a Completed order)

        (await c.ChecklistAsync(order, confirm: true)).CanClose.Should().BeTrue();
        var closed = await c.CloseOrderAsync(order, confirm: true);

        closed.Status.Should().Be(OrderStatus.Closed);
        var son = c.Mat(order, "NVL-SON01");
        (son.ConsumedQty, son.SettledIssueQty, son.NetIssuedQty).Should().Be((51.5m, 51.5m, 51.5m));
        order.History.Select(h => h.ToStatus).Should().Equal(OrderStatus.Released, OrderStatus.InProgress, OrderStatus.Completed, OrderStatus.Closed);
        (await Refused(() => c.ReopenOrderAsync(order))).RuleName.Should().Be(MfgErrors.Po014);
        (await Refused(() => c.ReturnAsync(order, [c.Line(order, "NVL-SON01", 1m)]))).RuleName.Should().Be(MfgErrors.Po014);
        (await Refused(() => c.CompleteOrderAsync(order))).RuleName.Should().Be(MfgErrors.Po014);
    }

    [Fact] // TC-PO-040
    public async Task Only_a_completed_order_can_be_closed()
    {
        var (c, order) = await ReadyAsync();

        (await Refused(() => c.CloseOrderAsync(order))).RuleName.Should().Be(MfgErrors.Po002);
    }

    [Fact]
    public async Task A_return_is_accepted_on_a_completed_order_but_an_issue_is_not()
    {
        var (c, order) = await ReadyAsync();
        await c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 5m)]);
        await c.ProduceAsync(order, 100);
        await c.ReceiveMainAsync(order, 100);

        (await c.ReturnAsync(order, [c.Line(order, "NVL-KEO01", 2m)])).Document.Status.Should().Be(StockDocumentStatus.Posted);
        (await Refused(() => c.IssueAsync(order, [c.Line(order, "NVL-KEO01", 1m)]))).RuleName.Should().Be(MfgErrors.Mat001);
    }

    [Fact]
    public async Task A_held_lot_that_is_released_no_longer_blocks_the_close()
    {
        var (c, order) = await ReadyAsync();
        await c.ProduceAsync(order, 100);
        var receipt = (await c.ReceiveMainAsync(order, 100)).Document;
        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeFalse();

        await c.QcReleaseAsync(receipt.Id);

        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeTrue();
    }
}
