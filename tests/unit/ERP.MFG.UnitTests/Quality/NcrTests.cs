using ERP.MFG.Application.Features.Inspections;
using ERP.MFG.Application.Features.Ncr;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Orders;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Quality;

internal static class NcrHelpers
{
    public static Task<NcrDto> GetNcrAsync(this OpsCtx c, Guid id) => new GetNcrHandler(c.Access, c.F.NcrRepo, c.Repo).Handle(new GetNcrQuery(id), default);

    public static Task<NcrDto> DispositionsAsync(this OpsCtx c, Guid id, IUserContextService? user, params DispositionBody[] lines)
        => new SaveNcrDispositionsHandler(c.Access, c.F.NcrRepo, c.Repo, c.F.ReasonRepo, c.PlantTime, user ?? c.User, c.F.Uow).Handle(new SaveNcrDispositionsCommand(id, lines), default);

    public static Task<NcrExecuteResultDto> ExecuteNcrAsync(this OpsCtx c, Guid id, int lineNo, ExecuteDispositionBody? options = null)
        => new ExecuteNcrDispositionHandler(c.Access, c.F.NcrRepo, c.Repo, c.Stock, c.Quarantine, new ReworkOrderFactory(c.Builder(), c.Repo, c.Numbers, c.PlantTime), c.F.ReasonRepo, c.Ncr, c.Closer,
                c.PlantTime, c.User, c.F.Uow)
            .Handle(new ExecuteNcrDispositionCommand(id, lineNo, options), default);

    public static Task<NcrDto> ManualNcrAsync(this OpsCtx c, ProductionOrder order, Guid reportId, decimal qty, string description = "Scratched panels")
        => new CreateManualNcrHandler(c.Access, c.Repo, c.Reports, c.F.NcrRepo, c.Numbers, c.PlantTime, c.User, c.F.Uow).Handle(new CreateManualNcrCommand(order.Id, reportId, qty, description), default);

    public static IUserContextService Approver() => StockCtx.UserWith(PermissionConstants.Manufacturing_QcApproveDeviation);

    public static DispositionBody Rework(decimal qty) => new(DispositionType.Rework, qty, null, null);
    public static DispositionBody Scrap(OpsCtx c, decimal qty) => new(DispositionType.Scrap, qty, c.ScrapReason.Id, null);
    public static DispositionBody UseAsIs(decimal qty) => new(DispositionType.UseAsIs, qty, null, "customer accepts");
}

public class NcrRulesTests
{
    private static DispositionInput Line(DispositionType type, decimal qty, Guid? reason = null) => new(type, qty, reason, null);

    [Fact] // TC-NCR-002
    public void The_lines_must_add_up_to_the_nonconforming_quantity()
    {
        var act = () => NcrRules.CheckDispositions([Line(DispositionType.Rework, 10), Line(DispositionType.Scrap, 5, Guid.NewGuid()), Line(DispositionType.UseAsIs, 80)], 100m, false, true);

        var refused = act.Should().Throw<BusinessRuleException>().Which;
        refused.RuleName.Should().Be(MfgErrors.Ncr001);
        refused.Message.Should().Contain("100");
    }

    [Fact] // TC-NCR-003
    public void Rework_10_scrap_5_and_use_as_is_85_are_accepted_for_an_approver()
        => NcrRules.CheckDispositions([Line(DispositionType.Rework, 10), Line(DispositionType.Scrap, 5, Guid.NewGuid()), Line(DispositionType.UseAsIs, 85)], 100m, false, true);

    [Fact] // TC-NCR-004
    public void Use_as_is_needs_the_approval_right()
    {
        var act = () => NcrRules.CheckDispositions([Line(DispositionType.UseAsIs, 100)], 100m, false, canApproveDeviation: false);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Ncr002);
    }

    [Fact]
    public void Output_held_by_an_in_process_inspection_cannot_be_reworked()
    {
        var act = () => NcrRules.CheckDispositions([Line(DispositionType.Rework, 40)], 40m, inProcess: true, true);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Ncr003);
        NcrRules.CheckDispositions([Line(DispositionType.Scrap, 10, Guid.NewGuid()), Line(DispositionType.UseAsIs, 30)], 40m, inProcess: true, true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_line_needs_a_quantity_above_zero_and_a_scrap_line_needs_its_reason(double qty)
    {
        var bad = () => NcrRules.CheckDispositions([Line(DispositionType.Rework, (decimal)qty), Line(DispositionType.Rework, 100)], 100m, false, true);
        bad.Should().Throw<BusinessRuleException>();

        var noReason = () => NcrRules.CheckDispositions([Line(DispositionType.Scrap, 100)], 100m, false, true);
        noReason.Should().Throw<BusinessRuleException>();
        var none = () => NcrRules.CheckDispositions([], 100m, false, true);
        none.Should().Throw<BusinessRuleException>();
    }

    [Fact] // BR-RW-02
    public void A_rework_lot_is_the_root_lot_plus_the_smallest_free_r_number_and_never_a_chain()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TP-261104-01" };

        NcrRules.ReworkLotNumber("TP-261104-01", taken).Should().Be("TP-261104-01-R1");
        taken.Add("TP-261104-01-R1");
        NcrRules.ReworkLotNumber("TP-261104-01", taken).Should().Be("TP-261104-01-R2");
        NcrRules.ReworkLotNumber("TP-261104-01-R1", taken).Should().Be("TP-261104-01-R2", "the lot of a rework is reworked into the next R number of the root, not X-R1-R1");
        NcrRules.RootLotNumber("TP-261104-01-R2-R3").Should().Be("TP-261104-01");
    }
}

public class NcrFlowTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> act) => (await act.Should().ThrowAsync<BusinessRuleException>()).Which;

    /// <summary>TP-BAN01 (lot controlled, final inspection with an approved plan), an order of 100 that is produced and received, and a FAILED final inspection of the lot.</summary>
    private static async Task<(OpsCtx C, ProductionOrder Order, MaterialTransaction Receipt, Inspection Inspection, NcrDto Ncr)> FailedAsync()
    {
        var c = new OpsCtx();
        await c.SeedAllAsync();
        c.StockDs01();
        c.FinishedGoodIs(lotControlled: true, fqc: true);
        await c.LinkFqcPlanAsync();
        var draft = await c.CreateOrderAsync("TP-BAN01", 100);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        await c.ProduceAsync(order, 100);
        var document = (await c.ReceiveMainAsync(order, 100)).Document;
        var inspection = c.F.Inspections.Single();
        var failed = await c.SubmitAsync(inspection.Id, InspectionHelpers.Values(await c.GetInspectionAsync(inspection.Id), 753m));
        var ncr = await c.GetNcrAsync(failed.Inspection.NcrId!.Value);
        return (c, order, c.Documents.Docs.Single(d => d.Id == document.Id), inspection, ncr);
    }

    private static async Task<NcrDto> PlanAsync(OpsCtx c, NcrDto ncr, bool approver = true)
        => await c.DispositionsAsync(ncr.Ncr.Id, approver ? NcrHelpers.Approver() : null, NcrHelpers.Rework(10), NcrHelpers.Scrap(c, 5), NcrHelpers.UseAsIs(85));

    private static decimal LotQty(OpsCtx c, Guid itemId, Guid lotId) => c.Stock.Balances[itemId].Single(b => b.LotId == lotId).Quantity;

    // ── creation ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-NCR-001
    public async Task A_failed_final_inspection_creates_an_open_ncr_for_the_whole_lot()
    {
        var (c, order, receipt, inspection, ncr) = await FailedAsync();

        var n = ncr.Ncr;
        (n.Status, n.NonconformingQty, n.SourceType, n.InspectionId, n.OrderId, n.IsInProcess, n.IsRecordOnly).Should().Be((NcrStatus.Open, 100m, NcrSourceType.Inspection, inspection.Id, order.Id, false, false));
        n.NcrNo.Should().MatchRegex(@"^NCR-\d{4}-\d{5}$");
        var line = receipt.ReceiptLines.Single();
        (n.LotId, n.LotNumber).Should().Be((line.PostedLotId, line.LotNumber));
        ncr.Dispositions.Should().BeEmpty();
        (await c.GetInspectionAsync(inspection.Id)).Inspection.NcrNo.Should().Be(n.NcrNo);
        c.F.Ncrs.Should().ContainSingle();
    }

    [Fact]
    public async Task A_passed_inspection_creates_no_ncr_and_asking_twice_does_not_create_a_second_one()
    {
        var (c, _, _, inspection, _) = await FailedAsync();
        await c.Ncr.CreateForFailedInspectionAsync(inspection, "again");
        c.F.Ncrs.Should().ContainSingle();

        var c2 = new OpsCtx();
        await c2.SeedAllAsync();
        c2.StockDs01();
        c2.FinishedGoodIs(lotControlled: true, fqc: true);
        await c2.LinkFqcPlanAsync();
        var draft = await c2.CreateOrderAsync("TP-BAN01", 100);
        await c2.ReleaseAsync(draft.Id);
        await c2.ProduceAsync(c2.Stored(draft.Id), 100);
        await c2.ReceiveMainAsync(c2.Stored(draft.Id), 100);
        var i = c2.F.Inspections.Single();
        await c2.SubmitAsync(i.Id, InspectionHelpers.Values(await c2.GetInspectionAsync(i.Id)));

        c2.F.Ncrs.Should().BeEmpty();
    }

    // ── dispositions ──────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-NCR-002 / 003 / 004
    public async Task Dispositions_must_add_up_and_use_as_is_needs_the_approval_right()
    {
        var (c, _, _, _, ncr) = await FailedAsync();

        (await Refused(() => c.DispositionsAsync(ncr.Ncr.Id, NcrHelpers.Approver(), NcrHelpers.Rework(10), NcrHelpers.Scrap(c, 5), NcrHelpers.UseAsIs(80)))).RuleName.Should().Be(MfgErrors.Ncr001);
        (await Refused(() => PlanAsync(c, ncr, approver: false))).RuleName.Should().Be(MfgErrors.Ncr002);

        var saved = await PlanAsync(c, ncr);

        saved.Ncr.Status.Should().Be(NcrStatus.InDisposition);
        saved.Dispositions.Select(d => (d.LineNo, d.Type, d.Qty)).Should().Equal((1, DispositionType.Rework, 10m), (2, DispositionType.Scrap, 5m), (3, DispositionType.UseAsIs, 85m));
        saved.Dispositions.Single(d => d.Type == DispositionType.UseAsIs).ApprovedBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_scrap_line_needs_an_active_scrap_reason_of_the_right_type()
    {
        var (c, _, _, _, ncr) = await FailedAsync();

        var refused = await Refused(() => c.DispositionsAsync(ncr.Ncr.Id, null, new DispositionBody(DispositionType.Scrap, 100, c.ReworkReason.Id, null)));

        refused.RuleName.Should().Be("MFG_VALIDATION");
    }

    // ── execution ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-NCR-005
    public async Task Executing_a_scrap_line_scraps_the_quantity_from_the_held_lot_with_the_ncr_in_the_reason()
    {
        var (c, order, _, _, ncr) = await FailedAsync();
        var item = c.Items["TP-BAN01"].Id;
        await PlanAsync(c, ncr);

        var done = await c.ExecuteNcrAsync(ncr.Ncr.Id, 2);

        var move = c.Stock.Movements.Single(m => m.Type == "ProductionScrap");
        move.Key.Should().Be($"{ncr.Ncr.NcrNo}#2");
        move.Quantity.Should().Be(5m);
        c.Stock.Requests.Last(r => r.MovementType == "ProductionScrap").Reason.Should().Contain(ncr.Ncr.NcrNo);
        LotQty(c, item, ncr.Ncr.LotId!.Value).Should().Be(95m);
        done.Ncr.Dispositions.Single(d => d.LineNo == 2).IsExecuted.Should().BeTrue();
        done.Ncr.Ncr.Status.Should().Be(NcrStatus.InDisposition);
        (await Refused(() => c.ExecuteNcrAsync(ncr.Ncr.Id, 2))).RuleName.Should().Be(MfgErrors.Ncr004);
    }

    [Fact] // TC-NCR-006
    public async Task Executing_use_as_is_moves_the_quantity_to_the_finished_goods_warehouse_and_releases_it_as_its_own_lot()
    {
        var (c, order, _, _, ncr) = await FailedAsync();
        var item = c.Items["TP-BAN01"].Id;
        await PlanAsync(c, ncr);

        var done = await c.ExecuteNcrAsync(ncr.Ncr.Id, 3);

        var line = done.Ncr.Dispositions.Single(d => d.LineNo == 3);
        line.IsExecuted.Should().BeTrue();
        (line.ResultLotId == ncr.Ncr.LotId).Should().BeFalse("a part of the lot leaves as a sub-lot");
        var sub = c.Stock.Balances[item].Single(b => b.LotId == line.ResultLotId);
        (sub.Quantity, sub.LotStatus, sub.WarehouseId).Should().Be((85m, "Available", order.OutputWarehouseId));
        LotQty(c, item, ncr.Ncr.LotId!.Value).Should().Be(15m);
        c.Stock.Movements.Select(m => m.Key).Should().Contain([$"{ncr.Ncr.NcrNo}#3Q1", $"{ncr.Ncr.NcrNo}#3Q2"]);
    }

    [Fact]
    public async Task A_warehouse_outage_executes_nothing_and_the_second_try_moves_the_stock_exactly_once()
    {
        var (c, _, _, _, ncr) = await FailedAsync();
        await PlanAsync(c, ncr);
        c.Stock.Down = true;

        (await Refused(() => c.ExecuteNcrAsync(ncr.Ncr.Id, 3))).RuleName.Should().Be(MfgErrors.Int001);
        c.F.Ncrs.Single().Dispositions.Single(d => d.LineNo == 3).IsExecuted.Should().BeFalse();

        c.Stock.Down = false;
        await c.ExecuteNcrAsync(ncr.Ncr.Id, 3);
        c.Stock.Movements.Count(m => m.Key.EndsWith("#3Q1")).Should().Be(1);
        c.Stock.Movements.Count(m => m.Key.EndsWith("#3Q2")).Should().Be(1);
    }

    [Fact]
    public async Task The_whole_lot_as_is_moves_the_lot_itself_and_the_lines_can_be_executed_in_any_order()
    {
        var (c, order, _, _, ncr) = await FailedAsync();
        var item = c.Items["TP-BAN01"].Id;
        await c.DispositionsAsync(ncr.Ncr.Id, NcrHelpers.Approver(), NcrHelpers.UseAsIs(100));

        var done = await c.ExecuteNcrAsync(ncr.Ncr.Id, 1);

        done.Ncr.Dispositions.Single().ResultLotId.Should().Be(ncr.Ncr.LotId);
        var row = c.Stock.Balances[item].Single(b => b.LotId == ncr.Ncr.LotId);
        (row.LotStatus, row.WarehouseId, row.Quantity).Should().Be(("Available", order.OutputWarehouseId, 100m));
        done.Ncr.Ncr.Status.Should().Be(NcrStatus.Closed);
    }

    [Fact] // TC-NCR-007 (scrap + as-is part) and the settling of the receipt line
    public async Task Executing_every_line_closes_the_ncr_and_clears_the_quality_hold_of_the_order()
    {
        var (c, order, receipt, _, ncr) = await FailedAsync();
        await c.DispositionsAsync(ncr.Ncr.Id, NcrHelpers.Approver(), NcrHelpers.Scrap(c, 5), NcrHelpers.UseAsIs(95));
        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeFalse();

        await c.ExecuteNcrAsync(ncr.Ncr.Id, 1);
        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeFalse("one line is still open");
        var done = await c.ExecuteNcrAsync(ncr.Ncr.Id, 2);

        done.Ncr.Ncr.Status.Should().Be(NcrStatus.Closed);
        done.Ncr.Ncr.ClosedAt.Should().NotBeNull();
        receipt.ReceiptLines.Single().QcReleasedAt.Should().NotBeNull();
        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeTrue();
        c.F.Ncrs.Single().AllDispositions.Should().OnlyContain(d => d.IsExecuted);
        (await Refused(() => c.DispositionsAsync(ncr.Ncr.Id, null, NcrHelpers.Scrap(c, 100)))).RuleName.Should().Be(MfgErrors.Ncr004);
    }

    [Fact]
    public async Task A_started_line_cannot_be_replaced()
    {
        var (c, _, _, _, ncr) = await FailedAsync();
        await PlanAsync(c, ncr);
        await c.ExecuteNcrAsync(ncr.Ncr.Id, 2);

        (await Refused(() => PlanAsync(c, ncr))).RuleName.Should().Be(MfgErrors.Ncr004);
    }

    // ── rework order ──────────────────────────────────────────────────────────────────────────────────────

    [Fact] // TC-RW-001
    public async Task A_rework_line_makes_a_draft_rework_order_with_the_held_lot_as_its_only_material()
    {
        var (c, order, _, _, ncr) = await FailedAsync();
        await PlanAsync(c, ncr);

        var done = await c.ExecuteNcrAsync(ncr.Ncr.Id, 1);

        done.ReworkOrderId.Should().NotBeNull();
        var rework = c.Stored(done.ReworkOrderId!.Value);
        (rework.OrderType, rework.SourceType, rework.SourceRefId, rework.ParentOrderId, rework.IsRework, rework.Status).Should().Be((OrderType.Rework, OrderSourceType.Ncr, ncr.Ncr.Id, order.Id, true, OrderStatus.Draft));
        (rework.ItemId, rework.Quantity, rework.BomId).Should().Be((order.ItemId, 10m, (Guid?)null));
        rework.Operations.Should().BeEmpty();
        var material = rework.Materials.Single();
        (material.ComponentItemId, material.RequiredQty, material.SourceLotId, material.SourceLotNumber, material.IssueMethod, material.WarehouseId)
            .Should().Be((order.ItemId, 10m, ncr.Ncr.LotId, ncr.Ncr.LotNumber, IssueMethod.Manual, c.Plant.QuarantineWarehouseId));
        rework.Notes.Should().Contain(ncr.Ncr.NcrNo);

        var line = done.Ncr.Dispositions.Single(d => d.LineNo == 1);
        (line.IsStarted, line.IsExecuted, line.ReworkOrderNo).Should().Be((true, false, rework.OrderNo));
        (await Refused(() => c.ExecuteNcrAsync(ncr.Ncr.Id, 1))).RuleName.Should().Be(MfgErrors.Ncr004);
        (await Refused(() => PlanAsync(c, ncr))).RuleName.Should().Be(MfgErrors.Ncr004);
    }

    [Fact]
    public async Task A_rework_order_is_released_without_a_reservation_even_when_a_shortage_would_block_a_normal_release()
    {
        var (c, _, _, _, ncr) = await FailedAsync();
        await PlanAsync(c, ncr);
        var rework = c.Stored((await c.ExecuteNcrAsync(ncr.Ncr.Id, 1)).ReworkOrderId!.Value);
        c.PlantSettings.BlockReleaseOnShortage = true;
        var reservesBefore = c.Stock.Reserves;

        var released = await c.ReleaseAsync(rework.Id);

        released.Shortages.Should().BeEmpty();
        c.Stored(rework.Id).Status.Should().Be(OrderStatus.Released);
        c.Stock.Reserves.Should().Be(reservesBefore, "the held lot is covered by the lot itself and is never reserved");
        c.Stored(rework.Id).Materials.Single().ReservedQty.Should().Be(0m);
    }

    [Fact] // TC-RW-002
    public async Task Issuing_the_lot_to_the_rework_order_asks_the_warehouse_for_a_held_lot_issue_with_the_order_in_the_reason()
    {
        var (c, _, _, _, ncr) = await FailedAsync();
        var item = c.Items["TP-BAN01"].Id;
        await c.DispositionsAsync(ncr.Ncr.Id, null, NcrHelpers.Rework(100));
        var rework = c.Stored((await c.ExecuteNcrAsync(ncr.Ncr.Id, 1)).ReworkOrderId!.Value);
        await c.ReleaseAsync(rework.Id);
        rework = c.Stored(rework.Id);

        var other = Guid.NewGuid();
        (await Refused(() => c.IssueAsync(rework, [c.Line(rework, "TP-BAN01", 10, other)]))).RuleName.Should().Be(MfgErrors.Mat005);
        var issued = await c.IssueAsync(rework, [c.Line(rework, "TP-BAN01", 100)]);

        issued.IsPending.Should().BeFalse();
        var request = c.Stock.Requests.Last(r => r.MovementType == "ProductionIssue");
        (request.AllowHeldLot, request.LotId, request.ReservationId).Should().Be((true, ncr.Ncr.LotId, (Guid?)null));
        request.Reason.Should().Contain(rework.OrderNo).And.Contain(ncr.Ncr.NcrNo);
        LotQty(c, item, ncr.Ncr.LotId!.Value).Should().Be(0m);
        c.Stored(rework.Id).Materials.Single().IssuedQty.Should().Be(100m);
    }

    [Fact] // TC-RW-003, TC-NCR-007
    public async Task Receiving_the_reworked_goods_makes_an_r_lot_on_hold_with_a_new_inspection_and_closes_the_ncr()
    {
        var (c, order, receipt, _, ncr) = await FailedAsync();
        var item = c.Items["TP-BAN01"].Id;
        await PlanAsync(c, ncr);
        await c.ExecuteNcrAsync(ncr.Ncr.Id, 2);
        await c.ExecuteNcrAsync(ncr.Ncr.Id, 3);
        var rework = c.Stored((await c.ExecuteNcrAsync(ncr.Ncr.Id, 1)).ReworkOrderId!.Value);
        await c.ReleaseAsync(rework.Id);
        rework = c.Stored(rework.Id);
        await c.IssueAsync(rework, [c.Line(rework, "TP-BAN01", 10)]);

        (await Refused(() => c.ReceiveMainAsync(rework, 10, lot: "MY-OWN-LOT"))).RuleName.Should().Be("MFG_VALIDATION");
        var received = await c.ReceiveMainAsync(rework, 10);

        var reworkLine = received.Document.Lines.Single(l => l.Kind == StockLineKind.Receipt);
        reworkLine.LotNumber.Should().Be(ncr.Ncr.LotNumber + "-R1");
        reworkLine.IsQcHold.Should().BeTrue();
        var row = c.Stock.Balances[item].Single(b => b.LotNumber == reworkLine.LotNumber);
        (row.WarehouseId, row.LotStatus).Should().Be((c.Plant.QuarantineWarehouseId, "QcHold"));
        c.F.Inspections.Should().ContainSingle(i => i.LotNumber == reworkLine.LotNumber && i.Status == InspectionStatus.Pending && i.OrderId == rework.Id);

        c.Stored(rework.Id).Status.Should().Be(OrderStatus.Completed, "all of the quantity was received");
        var closed = await c.GetNcrAsync(ncr.Ncr.Id);
        closed.Ncr.Status.Should().Be(NcrStatus.Closed);
        closed.Dispositions.Should().OnlyContain(d => d.IsExecuted);
        receipt.ReceiptLines.Single().QcReleasedAt.Should().NotBeNull();
        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeTrue();
    }

    [Fact]
    public async Task A_second_rework_of_the_same_lot_gets_the_next_r_number()
    {
        var (c, _, _, _, ncr) = await FailedAsync();
        c.Stock.DepletedLotNumbers[c.Items["TP-BAN01"].Id] = [ncr.Ncr.LotNumber + "-R1"];
        await c.DispositionsAsync(ncr.Ncr.Id, null, NcrHelpers.Rework(100));
        var rework = c.Stored((await c.ExecuteNcrAsync(ncr.Ncr.Id, 1)).ReworkOrderId!.Value);
        await c.ReleaseAsync(rework.Id);
        rework = c.Stored(rework.Id);
        await c.IssueAsync(rework, [c.Line(rework, "TP-BAN01", 100)]);

        var received = await c.ReceiveMainAsync(rework, 100);

        received.Document.Lines.Single(l => l.Kind == StockLineKind.Receipt).LotNumber.Should().Be(ncr.Ncr.LotNumber + "-R2");
    }

    [Fact]
    public async Task Cancelling_the_draft_rework_order_frees_the_line_to_be_executed_again()
    {
        var (c, _, _, _, ncr) = await FailedAsync();
        await PlanAsync(c, ncr);
        var first = (await c.ExecuteNcrAsync(ncr.Ncr.Id, 1)).ReworkOrderId!.Value;

        await c.CancelAsync(first, c.CancelReason.Id);

        c.F.Ncrs.Single().AllDispositions.Single(d => d.LineNo == 1).ReworkOrderId.Should().BeNull();
        var second = (await c.ExecuteNcrAsync(ncr.Ncr.Id, 1)).ReworkOrderId;
        second.Should().NotBe(first);
    }

    [Fact]
    public async Task A_rework_order_cannot_be_edited_as_a_draft_or_given_another_quantity()
    {
        var (c, _, _, _, ncr) = await FailedAsync();
        await PlanAsync(c, ncr);
        var rework = c.Stored((await c.ExecuteNcrAsync(ncr.Ncr.Id, 1)).ReworkOrderId!.Value);

        (await Refused(() => c.UpdateHandler().Handle(new UpdateOrderCommand(rework.Id, 12, rework.DueDate, 3, null, null, null, null), default))).RuleName.Should().Be("MFG_VALIDATION");
        await c.ReleaseAsync(rework.Id);
        (await Refused(() => c.ChangeQuantityAsync(rework.Id, 12))).RuleName.Should().Be("MFG_VALIDATION");
    }

    // ── re-inspection meets the NCR ───────────────────────────────────────────────────────────────────────

    [Fact] // TC-QC-013 with an NCR
    public async Task A_re_inspection_before_any_line_started_cancels_the_ncr_and_a_pass_releases_the_lot()
    {
        var (c, _, receipt, inspection, ncr) = await FailedAsync();
        await PlanAsync(c, ncr);

        var again = await c.ReinspectAsync(inspection.Id);
        await c.SubmitAsync(again.Inspection.Id, InspectionHelpers.Values(again));

        c.F.Ncrs.Single().Status.Should().Be(NcrStatus.Cancelled);
        receipt.ReceiptLines.Single().QcReleasedAt.Should().NotBeNull();
        (await c.GetNcrAsync(ncr.Ncr.Id)).Ncr.Status.Should().Be(NcrStatus.Cancelled);
    }

    [Fact]
    public async Task A_re_inspection_after_a_line_started_is_refused_in_favour_of_the_ncr_and_a_new_failure_makes_a_new_ncr()
    {
        var (c, _, _, inspection, ncr) = await FailedAsync();
        await PlanAsync(c, ncr);
        await c.ExecuteNcrAsync(ncr.Ncr.Id, 2);

        (await Refused(() => c.ReinspectAsync(inspection.Id))).RuleName.Should().Be(MfgErrors.Ncr004);
        inspection.ReinspectedById.Should().BeNull("nothing was created");

        var (c2, _, _, inspection2, ncr2) = await FailedAsync();
        var again = await c2.ReinspectAsync(inspection2.Id);
        var failedAgain = await c2.SubmitAsync(again.Inspection.Id, InspectionHelpers.Values(again, 753m));
        failedAgain.Inspection.NcrId.Should().NotBe(ncr2.Ncr.Id);
        c2.F.Ncrs.Select(n => n.Status).Should().BeEquivalentTo([NcrStatus.Cancelled, NcrStatus.Open]);
    }

    [Fact]
    public async Task An_open_ncr_keeps_the_order_from_closing_with_its_own_message()
    {
        var (c, order, _, _, _) = await FailedAsync();

        var checklist = await c.ChecklistAsync(order);

        var quality = checklist.Conditions.Single(x => x.Code == "QUALITY_HOLD");
        quality.Met.Should().BeFalse();
        quality.Message.Should().Contain("1");
        checklist.CanClose.Should().BeFalse();
    }
}

public class NcrInProcessAndManualTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> act) => (await act.Should().ThrowAsync<BusinessRuleException>()).Which;

    private static async Task<(OpsCtx C, ProductionOrder Order, NcrDto Ncr)> FailedIpqcAsync()
    {
        var c = new OpsCtx();
        await c.SeedAllAsync();
        c.StockDs01();
        var draft = await c.CreateOrderAsync("TP-BAN01", 100);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        await c.LinkIpqcPlanAsync(order, 10);
        await c.ReportAsync(order, 10, 40);
        var first = c.F.Inspections.Single();
        var failed = await c.SubmitAsync(first.Id, InspectionHelpers.Values(await c.GetInspectionAsync(first.Id), 750m, "Appearance"));
        return (c, order, await c.GetNcrAsync(failed.Inspection.NcrId!.Value));
    }

    [Fact]
    public async Task A_failed_in_process_inspection_creates_an_ncr_for_the_held_quantity_that_cannot_be_reworked()
    {
        var (c, _, ncr) = await FailedIpqcAsync();

        (ncr.Ncr.IsInProcess, ncr.Ncr.NonconformingQty, ncr.Ncr.OperationSeq, ncr.Ncr.LotId).Should().Be((true, 40m, 10, (Guid?)null));
        (await Refused(() => c.DispositionsAsync(ncr.Ncr.Id, NcrHelpers.Approver(), NcrHelpers.Rework(40)))).RuleName.Should().Be(MfgErrors.Ncr003);
    }

    [Fact]
    public async Task Scrapping_part_of_the_held_output_and_accepting_the_rest_frees_the_next_operation_for_what_is_left()
    {
        var (c, order, ncr) = await FailedIpqcAsync();
        await c.DispositionsAsync(ncr.Ncr.Id, NcrHelpers.Approver(), NcrHelpers.Scrap(c, 10), NcrHelpers.UseAsIs(30));
        var moves = c.Stock.MovementCalls;

        await c.ExecuteNcrAsync(ncr.Ncr.Id, 1);
        var op = c.Op(order, 10);
        (op.GoodQty, op.ScrapQty, op.IpqcHeldQty, op.NetOutputQty).Should().Be((30m, 10m, 30m, 0m));
        order.ScrappedQty.Should().Be(10m);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Pending, "the 30 are still held until they are accepted");

        var done = await c.ExecuteNcrAsync(ncr.Ncr.Id, 2);

        (op.IpqcHeldQty, op.IpqcPassedQty, op.NetOutputQty).Should().Be((0m, 30m, 30m));
        done.Ncr.Ncr.Status.Should().Be(NcrStatus.Closed);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready);
        c.Stock.MovementCalls.Should().Be(moves, "work in process is not in stock: no Warehouse movement");
        await c.ReportAsync(order, 20, 30);
        (await Refused(() => c.ReportAsync(order, 20, 1))).RuleName.Should().Be(MfgErrors.Op004);
    }

    [Fact]
    public async Task A_manual_ncr_from_a_report_is_a_record_and_its_lines_are_ticked_off_without_moving_anything()
    {
        var c = new OpsCtx();
        await c.SeedAllAsync();
        c.StockDs01();
        var draft = await c.CreateOrderAsync("TP-BAN01", 100);
        await c.ReleaseAsync(draft.Id);
        var order = c.Stored(draft.Id);
        var report = (await c.ReportAsync(order, 10, 60, scrap: 2)).Report;

        (await Refused(() => c.ManualNcrAsync(order, report.Id, 63))).RuleName.Should().Be("MFG_VALIDATION");
        var ncr = await c.ManualNcrAsync(order, report.Id, 5);

        (ncr.Ncr.SourceType, ncr.Ncr.IsRecordOnly, ncr.Ncr.NonconformingQty, ncr.Ncr.OperationReportId, ncr.Ncr.OperationSeq).Should().Be((NcrSourceType.OperationReport, true, 5m, report.Id, 10));
        var moves = c.Stock.MovementCalls;

        await c.DispositionsAsync(ncr.Ncr.Id, NcrHelpers.Approver(), NcrHelpers.Rework(2), NcrHelpers.Scrap(c, 1), NcrHelpers.UseAsIs(2));
        await c.ExecuteNcrAsync(ncr.Ncr.Id, 1);
        await c.ExecuteNcrAsync(ncr.Ncr.Id, 2);
        var done = await c.ExecuteNcrAsync(ncr.Ncr.Id, 3);

        done.Ncr.Ncr.Status.Should().Be(NcrStatus.Closed);
        c.Stock.MovementCalls.Should().Be(moves);
        c.F.Ncrs.Should().ContainSingle();
    }
}
