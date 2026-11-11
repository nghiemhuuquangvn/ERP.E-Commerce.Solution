using ERP.MFG.Application.Features.Inspections;
using ERP.MFG.Application.Features.Receipts;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Orders;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Quality;

internal static class InspectionHelpers
{
    public static Task<InspectionDto> GetInspectionAsync(this OpsCtx c, Guid id)
        => new GetInspectionHandler(c.Access, c.F.InspectionRepo, c.F.PlanRepo).Handle(new GetInspectionQuery(id), default);

    public static Task<InspectionDto> SubmitAsync(this OpsCtx c, Guid id, IEnumerable<InspectionResultBody>? results = null, decimal? sample = null)
        => new SubmitInspectionHandler(c.Access, c.F.InspectionRepo, c.F.PlanRepo, c.Repo, c.Documents, c.Quarantine, c.Closer, c.PlantTime, c.User, c.F.Uow)
            .Handle(new SubmitInspectionCommand(id, sample, results?.ToList()), default);

    public static Task<InspectionDto> SaveResultsAsync(this OpsCtx c, Guid id, IEnumerable<InspectionResultBody> results, decimal? sample = null)
        => new SaveInspectionResultsHandler(c.Access, c.F.InspectionRepo, c.F.PlanRepo, c.F.Uow).Handle(new SaveInspectionResultsCommand(id, sample, results.ToList()), default);

    public static Task<InspectionDto> ReinspectAsync(this OpsCtx c, Guid id)
        => new ReinspectHandler(c.Access, c.F.InspectionRepo, c.F.PlanRepo, c.Numbers, c.PlantTime, c.F.Uow).Handle(new ReinspectCommand(id), default);

    /// <summary>Values for every characteristic of the inspection: a measurement gets <paramref name="length"/> on every sample unit, an attribute passes unless its name is in <paramref name="failing"/>.</summary>
    public static List<InspectionResultBody> Values(InspectionDto d, decimal length = 750m, params string[] failing)
    {
        var list = new List<InspectionResultBody>();
        foreach (var ch in d.Characteristics)
        {
            if (ch.Kind == CharacteristicKind.Measurement)
                for (var no = 1; no <= d.Units; no++) list.Add(new InspectionResultBody(ch.Id, no, length, null, null));
            else list.Add(new InspectionResultBody(ch.Id, 1, null, !failing.Contains(ch.Name), null));
        }
        return list;
    }

    /// <summary>The FQC plan of DS-01 (see PlanHelpers.SeedFqcPlanAsync) linked to the finished good, which also needs a final inspection.</summary>
    public static async Task<InspectionPlanDto> LinkFqcPlanAsync(this OpsCtx c, bool approve = true)
    {
        var plan = await c.SeedFqcPlanAsync(approve: approve);
        var item = c.Items["TP-BAN01"];
        var profile = c.F.Profiles.Single(p => p.ItemId == item.Id);
        profile.Update(item.Sku, item.Name, profile.ToValues() with { RequiresFqc = true, FqcInspectionPlanId = plan.Id });
        return plan;
    }

    /// <summary>An IPQC plan of the finished good (one critical attribute) pointed at by operation <paramref name="seq"/> of the order.</summary>
    public static async Task<InspectionPlanDto> LinkIpqcPlanAsync(this OpsCtx c, ProductionOrder order, int seq, bool approve = true, string code = "IPQC-BAN01")
    {
        var plan = await c.CreatePlanAsync("TP-BAN01", code, InspectionType.Ipqc, SamplingMethod.Fixed, 3, 0);
        await c.AddCharAsync(plan.Id, PlanHelpers.Attribute("Appearance", critical: true));
        if (approve) plan = await c.ApprovePlanAsync(plan.Id);
        typeof(ProductionOrderOperation).GetProperty(nameof(ProductionOrderOperation.IpqcInspectionPlanId))!.SetValue(order.OperationOf(seq), plan.Id);
        return plan;
    }
}

public class QualityRulesTests
{
    [Theory] // TC-QC-003 / 004 / 005
    [InlineData(SamplingMethod.Fixed, 5, 100, 5)]
    [InlineData(SamplingMethod.Fixed, 5, 3, 3)]
    [InlineData(SamplingMethod.Percent, 10, 101, 11)]
    [InlineData(SamplingMethod.Percent, 10, 100, 10)]
    [InlineData(SamplingMethod.Percent, 100, 7, 7)]
    [InlineData(SamplingMethod.Full, 0, 42, 42)]
    [InlineData(SamplingMethod.Fixed, 5, 0, 0)]
    public void The_sample_size_follows_the_method_and_never_exceeds_the_lot(SamplingMethod method, double value, double lot, double expected)
        => QualityRules.SampleSize(method, (decimal)value, (decimal)lot).Should().Be((decimal)expected);

    [Theory]
    [InlineData(0.4, 1)]
    [InlineData(5, 5)]
    [InlineData(5.2, 6)]
    public void A_part_of_a_unit_still_is_a_unit_to_look_at_and_there_is_always_one(double sample, int units)
        => QualityRules.Units((decimal)sample).Should().Be(units);

    private static (InspectionPlan Plan, Inspection Inspection) Fqc(int minorAllowed = 1, decimal lot = 100m)
    {
        var plan = InspectionPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "TP", "Table", "FQC-X", InspectionType.Fqc, 1, SamplingMethod.Fixed, 5, minorAllowed, new DateOnly(2026, 1, 1), null, null);
        plan.AddCharacteristic(new(10, "C1", CharacteristicKind.Measurement, 748, 752, "mm", true));
        plan.AddCharacteristic(new(20, "C2", CharacteristicKind.Attribute, null, null, null, true));
        plan.AddCharacteristic(new(30, "C3", CharacteristicKind.Attribute, null, null, null, false));
        plan.AddCharacteristic(new(40, "C4", CharacteristicKind.Attribute, null, null, null, false));
        var inspection = Inspection.CreateFqc("QI-2611-00001", plan, Guid.NewGuid(), "MO-1", Guid.NewGuid(), Guid.NewGuid(), plan.ItemId, "TP", "Table", Guid.NewGuid(), "LOT-1", lot);
        return (plan, inspection);
    }

    private static List<InspectionResultInput> Enter(InspectionPlan plan, decimal[] c1, params string[] failing)
    {
        var chars = plan.Characteristics.ToList();
        var list = c1.Select((v, i) => new InspectionResultInput(chars[0].Id, i + 1, v, null, null)).ToList();
        foreach (var c in chars.Skip(1)) list.Add(new InspectionResultInput(c.Id, 1, null, !failing.Contains(c.Name), null));
        return list;
    }

    [Fact]
    public void TC_QC_007_Every_value_in_range_and_every_attribute_passing_passes()
    {
        var (plan, i) = Fqc();
        i.SampleQty.Should().Be(5m);
        i.SaveDraft(plan, Enter(plan, [749, 750, 751, 752, 750.5m]));

        i.Evaluate(plan).Passed.Should().BeTrue();
    }

    [Fact]
    public void TC_QC_008_One_value_out_of_range_fails_the_critical_characteristic_and_the_lot()
    {
        var (plan, i) = Fqc();
        i.SaveDraft(plan, Enter(plan, [749, 750, 751, 753, 750.5m]));

        var outcome = i.Evaluate(plan);

        outcome.Passed.Should().BeFalse();
        outcome.FailedCritical.Should().Be(1);
    }

    [Fact]
    public void TC_QC_009_One_minor_defect_within_the_tolerance_still_passes()
    {
        var (plan, i) = Fqc(minorAllowed: 1);
        i.SaveDraft(plan, Enter(plan, [750, 750, 750, 750, 750], "C3"));

        var outcome = i.Evaluate(plan);

        (outcome.Passed, outcome.FailedMinor).Should().Be((true, 1));
    }

    [Fact]
    public void TC_QC_010_Two_minor_defects_beyond_the_tolerance_fail()
    {
        var (plan, i) = Fqc(minorAllowed: 1);
        i.SaveDraft(plan, Enter(plan, [750, 750, 750, 750, 750], "C3", "C4"));

        i.Evaluate(plan).Passed.Should().BeFalse();
    }

    [Fact]
    public void A_critical_attribute_that_fails_fails_the_lot_whatever_the_minor_tolerance()
    {
        var (plan, i) = Fqc(minorAllowed: 5);
        i.SaveDraft(plan, Enter(plan, [750, 750, 750, 750, 750], "C2"));

        i.Evaluate(plan).Passed.Should().BeFalse();
    }

    [Fact]
    public void TC_QC_011_A_missing_value_gives_no_verdict_and_names_the_characteristic()
    {
        var (plan, i) = Fqc();
        var values = Enter(plan, [750, 750, 750, 750, 750]).Where(r => r.CharacteristicId != plan.Characteristics.Last().Id).ToList();   // C4 left empty
        i.SaveDraft(plan, values);

        var act = () => i.Evaluate(plan);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Qc003);
    }

    [Fact]
    public void A_measurement_needs_a_value_on_every_sample_unit()
    {
        var (plan, i) = Fqc();
        i.SaveDraft(plan, Enter(plan, [750, 750, 750, 750]));          // only 4 of 5 units measured

        var act = () => i.Evaluate(plan);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Qc003);
    }

    [Fact]
    public void A_value_typed_again_replaces_the_first_one_and_a_wrong_sample_number_is_refused()
    {
        var (plan, i) = Fqc();
        var c1 = plan.Characteristics.First();
        i.SaveDraft(plan, [new InspectionResultInput(c1.Id, 1, 753m, null, null)]);
        i.SaveDraft(plan, [new InspectionResultInput(c1.Id, 1, 750m, null, null)]);

        i.Results.Should().ContainSingle(r => r.CharacteristicId == c1.Id).Which.IsPassed.Should().BeTrue();
        var act = () => i.SaveDraft(plan, [new InspectionResultInput(c1.Id, 6, 750m, null, null)]);
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void TC_QC_006_A_sample_size_other_than_the_plans_is_refused_with_the_right_figure()
    {
        var (_, i) = Fqc();

        var act = () => i.CheckSample(4m);

        act.Should().Throw<BusinessRuleException>().Which.Message.Should().Contain("5");
        i.CheckSample(5m);
        i.CheckSample(null);
    }
}

public class FinalInspectionTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> act) => (await act.Should().ThrowAsync<BusinessRuleException>()).Which;

    private static async Task<(OpsCtx C, ProductionOrder Order, InspectionPlanDto Plan)> ReadyAsync(bool plan = true, bool approve = true)
    {
        var c = new OpsCtx();
        await c.SeedAllAsync();
        c.StockDs01();
        c.FinishedGoodIs(lotControlled: true, fqc: true);
        InspectionPlanDto? p = plan ? await c.LinkFqcPlanAsync(approve) : null;
        var draft = await c.CreateOrderAsync("TP-BAN01", 100);
        await c.ReleaseAsync(draft.Id);
        return (c, c.Stored(draft.Id), p!);
    }

    private static async Task<(StockDocumentDto Document, Inspection Inspection)> ReceivedAsync(OpsCtx c, ProductionOrder order, decimal qty = 100)
    {
        await c.ProduceAsync(order, qty);
        var document = (await c.ReceiveMainAsync(order, qty)).Document;
        return (document, c.F.Inspections.SingleOrDefault()!);
    }

    [Fact]
    public async Task A_receipt_of_an_item_with_an_approved_plan_creates_one_pending_inspection_with_the_plans_sample()
    {
        var (c, order, plan) = await ReadyAsync();

        var (document, inspection) = await ReceivedAsync(c, order);

        inspection.Should().NotBeNull();
        (inspection.Type, inspection.Status, inspection.LotQty, inspection.SampleQty, inspection.PlanId).Should().Be((InspectionType.Fqc, InspectionStatus.Pending, 100m, 5m, plan.Id));
        inspection.DocNo.Should().MatchRegex(@"^QI-\d{4}-\d{5}$");
        var line = c.Documents.Docs.Single(d => d.Id == document.Id).ReceiptLines.Single();
        inspection.ReceiptLineId.Should().Be(line.Id);
        inspection.LotNumber.Should().Be(line.LotNumber);
        line.IsQcHold.Should().BeTrue();
    }

    [Fact]
    public async Task TC_QC_002_A_draft_plan_or_no_plan_creates_no_inspection_and_the_lot_stays_on_hold()
    {
        var (c, order, _) = await ReadyAsync(approve: false);
        var (document, inspection) = await ReceivedAsync(c, order);
        inspection.Should().BeNull();
        document.Lines.Single(l => l.Kind == StockLineKind.Receipt).IsQcHold.Should().BeTrue();

        var (c2, order2, _) = await ReadyAsync(plan: false);
        var (_, none) = await ReceivedAsync(c2, order2);
        none.Should().BeNull();
    }

    [Fact]
    public async Task Without_a_plan_the_interim_quality_release_still_works()
    {
        var (c, order, _) = await ReadyAsync(plan: false);
        var (document, _) = await ReceivedAsync(c, order);

        var released = await c.QcReleaseAsync(document.Id);

        released.Lines.Single(l => l.Kind == StockLineKind.Receipt).QcReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Applying_the_same_receipt_again_does_not_create_a_second_inspection()
    {
        var (c, order, _) = await ReadyAsync();
        var (document, _) = await ReceivedAsync(c, order);
        var stored = c.Documents.Docs.Single(d => d.Id == document.Id);

        await c.Quality.CreateFqcAsync(order, stored);

        c.F.Inspections.Should().HaveCount(1);
    }

    [Fact] // TC-QC-007
    public async Task A_pass_releases_the_lot_to_the_finished_goods_warehouse_and_makes_it_available()
    {
        var (c, order, _) = await ReadyAsync();
        var (document, inspection) = await ReceivedAsync(c, order);
        var key = document.Lines.Single(l => l.Kind == StockLineKind.Receipt).MovementKey;

        var d = await c.SubmitAsync(inspection.Id, InspectionHelpers.Values(await c.GetInspectionAsync(inspection.Id)));

        d.Inspection.Status.Should().Be(InspectionStatus.Passed);
        d.Inspection.ReleasedAt.Should().NotBeNull();
        var row = c.Stock.Balances[c.Items["TP-BAN01"].Id].Single();
        (row.LotStatus, row.WarehouseId).Should().Be(("Available", order.OutputWarehouseId));
        c.Stock.Movements.Select(m => m.Key).Should().Contain([key + "Q1", key + "Q2"]);
        c.Documents.Docs.Single(x => x.Id == document.Id).ReceiptLines.Single().QcReleasedAt.Should().NotBeNull();
        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeTrue();
    }

    [Fact] // TC-QC-008, TC-NCR-001 (the marker)
    public async Task A_critical_failure_keeps_the_lot_on_hold_asks_for_an_ncr_and_blocks_release_and_reversal()
    {
        var (c, order, _) = await ReadyAsync();
        var (document, inspection) = await ReceivedAsync(c, order);
        var full = await c.GetInspectionAsync(inspection.Id);

        var d = await c.SubmitAsync(inspection.Id, InspectionHelpers.Values(full, 753m));

        d.Inspection.Status.Should().Be(InspectionStatus.Failed);
        d.Inspection.NcrRequired.Should().BeTrue();
        var row = c.Stock.Balances[c.Items["TP-BAN01"].Id].Single();
        (row.LotStatus, row.WarehouseId).Should().Be(("QcHold", order.PlantQuarantine(c)));
        (await Refused(() => c.QcReleaseAsync(document.Id))).RuleName.Should().Be(MfgErrors.Qc004);
        (await Refused(() => c.ReverseReceiptAsync(order, document.Id))).RuleName.Should().Be(MfgErrors.Fg004);
        (await c.ChecklistAsync(order)).Conditions.Single(x => x.Code == "QUALITY_HOLD").Met.Should().BeFalse();
    }

    [Fact] // TC-QC-006 / 011 / 012
    public async Task A_wrong_sample_missing_values_and_an_edit_after_submit_are_refused()
    {
        var (c, order, _) = await ReadyAsync();
        var (_, inspection) = await ReceivedAsync(c, order);
        var full = await c.GetInspectionAsync(inspection.Id);
        var values = InspectionHelpers.Values(full);

        (await Refused(() => c.SubmitAsync(inspection.Id, values, sample: 4m))).RuleName.Should().Be(MfgErrors.Qc001);
        (await Refused(() => c.SubmitAsync(inspection.Id, values.Take(values.Count - 1)))).RuleName.Should().Be(MfgErrors.Qc003);
        inspection.Status.Should().Be(InspectionStatus.Pending);

        await c.SubmitAsync(inspection.Id, values);
        (await Refused(() => c.SaveResultsAsync(inspection.Id, values))).RuleName.Should().Be(MfgErrors.Qc002);
        (await Refused(() => c.SubmitAsync(inspection.Id, values))).RuleName.Should().Be(MfgErrors.Qc002);
    }

    [Fact]
    public async Task A_draft_is_kept_between_visits_and_the_verdict_is_given_on_submit()
    {
        var (c, order, _) = await ReadyAsync();
        var (_, inspection) = await ReceivedAsync(c, order);
        var full = await c.GetInspectionAsync(inspection.Id);
        var values = InspectionHelpers.Values(full);

        await c.SaveResultsAsync(inspection.Id, values.Take(3));
        (await c.GetInspectionAsync(inspection.Id)).Results.Should().HaveCount(3);
        inspection.Status.Should().Be(InspectionStatus.Pending);

        await c.SaveResultsAsync(inspection.Id, values.Skip(3));
        (await c.SubmitAsync(inspection.Id)).Inspection.Status.Should().Be(InspectionStatus.Passed);
    }

    [Fact]
    public async Task A_warehouse_outage_at_submit_saves_nothing_and_the_second_submit_releases_the_lot_once()
    {
        var (c, order, _) = await ReadyAsync();
        var (document, inspection) = await ReceivedAsync(c, order);
        var values = InspectionHelpers.Values(await c.GetInspectionAsync(inspection.Id));
        c.Stock.Down = true;

        (await Refused(() => c.SubmitAsync(inspection.Id, values))).RuleName.Should().Be(MfgErrors.Int001);
        inspection.Status.Should().Be(InspectionStatus.Pending);
        inspection.Results.Should().NotBeEmpty("the values stay as a draft");

        c.Stock.Down = false;
        await c.SubmitAsync(inspection.Id);
        var key = document.Lines.Single(l => l.Kind == StockLineKind.Receipt).MovementKey;
        c.Stock.Movements.Count(m => m.Key == key + "Q2").Should().Be(1);
        inspection.Status.Should().Be(InspectionStatus.Passed);
    }

    [Fact] // TC-QC-013
    public async Task A_failed_inspection_is_re_inspected_once_and_a_pass_releases_the_lot()
    {
        var (c, order, _) = await ReadyAsync();
        var (_, first) = await ReceivedAsync(c, order);
        await c.SubmitAsync(first.Id, InspectionHelpers.Values(await c.GetInspectionAsync(first.Id), 753m));

        var again = await c.ReinspectAsync(first.Id);

        again.Inspection.ReinspectionOfId.Should().Be(first.Id);
        again.Inspection.Status.Should().Be(InspectionStatus.Pending);
        first.ReinspectedById.Should().Be(again.Inspection.Id);
        (await Refused(() => c.ReinspectAsync(first.Id))).Message.Should().Contain("already");
        (await c.SubmitAsync(again.Inspection.Id, InspectionHelpers.Values(again))).Inspection.Status.Should().Be(InspectionStatus.Passed);
        c.Stock.Balances[c.Items["TP-BAN01"].Id].Single().LotStatus.Should().Be("Available");
    }

    [Fact]
    public async Task A_passed_or_pending_inspection_cannot_be_re_inspected()
    {
        var (c, order, _) = await ReadyAsync();
        var (_, inspection) = await ReceivedAsync(c, order);

        (await Refused(() => c.ReinspectAsync(inspection.Id))).RuleName.Should().Be(MfgErrors.Qc002);
        await c.SubmitAsync(inspection.Id, InspectionHelpers.Values(await c.GetInspectionAsync(inspection.Id)));
        (await Refused(() => c.ReinspectAsync(inspection.Id))).RuleName.Should().Be(MfgErrors.Qc002);
    }

    [Fact]
    public async Task The_interim_release_is_refused_for_an_inspected_line_but_not_for_a_cancelled_inspection()
    {
        var (c, order, _) = await ReadyAsync();
        var (document, inspection) = await ReceivedAsync(c, order);

        (await Refused(() => c.QcReleaseAsync(document.Id))).RuleName.Should().Be(MfgErrors.Qc004);
        inspection.Cancel();
        (await c.QcReleaseAsync(document.Id)).Lines.Single(l => l.Kind == StockLineKind.Receipt).QcReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_receipt_with_a_pending_inspection_can_be_reversed_and_its_inspection_is_cancelled()
    {
        var (c, order, _) = await ReadyAsync();
        var (document, inspection) = await ReceivedAsync(c, order);

        await c.ReverseReceiptAsync(order, document.Id);

        inspection.Status.Should().Be(InspectionStatus.Cancelled);
    }

    [Fact]
    public async Task A_receipt_whose_inspection_has_a_result_cannot_be_reversed_and_a_passed_one_neither()
    {
        var (c, order, _) = await ReadyAsync();
        var (document, inspection) = await ReceivedAsync(c, order);
        await c.SubmitAsync(inspection.Id, InspectionHelpers.Values(await c.GetInspectionAsync(inspection.Id)));

        var refused = await Refused(() => c.ReverseReceiptAsync(order, document.Id));

        refused.RuleName.Should().Be(MfgErrors.Fg004);
        refused.Message.Should().Contain(inspection.DocNo);
    }

    [Fact]
    public async Task The_receipt_of_a_reversal_document_creates_no_inspection_and_a_new_receipt_after_a_reversal_is_inspected_again()
    {
        var (c, order, _) = await ReadyAsync();
        var (document, _) = await ReceivedAsync(c, order, 40);
        await c.ReverseReceiptAsync(order, document.Id);
        c.F.Inspections.Should().ContainSingle(i => i.Status == InspectionStatus.Cancelled);

        await c.ProduceAsync(order, 60);
        await c.ReceiveMainAsync(order, 40);

        c.F.Inspections.Should().HaveCount(2).And.ContainSingle(i => i.Status == InspectionStatus.Pending);
    }
}

internal static class OrderQuarantine
{
    public static Guid PlantQuarantine(this ProductionOrder order, OpsCtx c) => c.Plant.QuarantineWarehouseId;
}

public class InProcessInspectionTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> act) => (await act.Should().ThrowAsync<BusinessRuleException>()).Which;

    private static async Task<(OpsCtx C, ProductionOrder Order)> ReadyAsync(bool vitUnbound = false)
    {
        var c = new OpsCtx { VitUnbound = vitUnbound };
        await c.SeedAllAsync();
        c.StockDs01();
        var draft = await c.CreateOrderAsync("TP-BAN01", 100);
        await c.ReleaseAsync(draft.Id);
        return (c, c.Stored(draft.Id));
    }

    [Fact] // TC-QC-015
    public async Task A_good_report_at_an_operation_with_a_plan_creates_a_pending_inspection_and_holds_the_quantity()
    {
        var (c, order) = await ReadyAsync();
        var plan = await c.LinkIpqcPlanAsync(order, 10);

        var report = (await c.ReportAsync(order, 10, 60)).Report;

        var inspection = c.F.Inspections.Single();
        (inspection.Type, inspection.Status, inspection.LotQty, inspection.OperationSeq, inspection.OperationReportId, inspection.PlanId)
            .Should().Be((InspectionType.Ipqc, InspectionStatus.Pending, 60m, 10, report.Id, plan.Id));
        var op = c.Op(order, 10);
        (op.GoodQty, op.IpqcHeldQty, op.NetOutputQty).Should().Be((60m, 60m, 0m));
        c.Op(order, 20).Status.Should().Be(OperationStatus.Pending, "nothing passed yet, so the next operation has no input");
    }

    [Fact]
    public async Task No_plan_or_a_draft_plan_means_no_inspection_and_the_next_operation_is_ready_as_before()
    {
        var (c, order) = await ReadyAsync();
        await c.ReportAsync(order, 10, 20);
        c.F.Inspections.Should().BeEmpty();
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready);

        await c.LinkIpqcPlanAsync(order, 10, approve: false);
        await c.ReportAsync(order, 10, 20);
        c.F.Inspections.Should().BeEmpty();
        c.Op(order, 10).IpqcHeldQty.Should().Be(0m);
    }

    [Fact] // TC-QC-014
    public async Task Only_the_passed_quantity_is_input_of_the_next_operation()
    {
        var (c, order) = await ReadyAsync();
        await c.LinkIpqcPlanAsync(order, 10);
        await c.ReportAsync(order, 10, 50);
        await c.ReportAsync(order, 10, 10);
        var first = c.F.Inspections.First();
        var passed = await c.SubmitAsync(first.Id, InspectionHelpers.Values(await c.GetInspectionAsync(first.Id)));

        passed.Inspection.Status.Should().Be(InspectionStatus.Passed);
        var op10 = c.Op(order, 10);
        (op10.IpqcHeldQty, op10.IpqcPassedQty, op10.NetOutputQty).Should().Be((10m, 50m, 50m));
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready);

        (await Refused(() => c.ReportAsync(order, 20, 51))).RuleName.Should().Be(MfgErrors.Op004);
        await c.ReportAsync(order, 20, 50);
    }

    [Fact]
    public async Task A_failed_inspection_keeps_the_hold_and_a_re_inspection_that_passes_frees_it()
    {
        var (c, order) = await ReadyAsync();
        await c.LinkIpqcPlanAsync(order, 10);
        await c.ReportAsync(order, 10, 40);
        var first = c.F.Inspections.Single();

        var failed = await c.SubmitAsync(first.Id, InspectionHelpers.Values(await c.GetInspectionAsync(first.Id), 750m, "Appearance"));

        failed.Inspection.NcrRequired.Should().BeTrue();
        c.Op(order, 10).IpqcHeldQty.Should().Be(40m);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Pending);

        var again = await c.ReinspectAsync(first.Id);
        await c.SubmitAsync(again.Inspection.Id, InspectionHelpers.Values(again));

        c.Op(order, 10).IpqcHeldQty.Should().Be(0m);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready);
    }

    [Fact]
    public async Task A_plan_approved_after_earlier_reports_does_not_hold_them()
    {
        var (c, order) = await ReadyAsync();
        await c.ReportAsync(order, 10, 30);
        await c.LinkIpqcPlanAsync(order, 10);

        await c.ReportAsync(order, 10, 20);

        var op = c.Op(order, 10);
        (op.GoodQty, op.IpqcHeldQty, op.NetOutputQty).Should().Be((50m, 20m, 30m));
    }

    [Fact]
    public async Task A_report_with_a_backflush_gets_its_inspection_only_when_it_is_posted_and_exactly_once()
    {
        var (c, order) = await ReadyAsync();
        await c.LinkIpqcPlanAsync(order, 10);                       // operation 10 backflushes the screws
        c.Stock.Intercept = (r, _) => r.MovementType == "ProductionIssue" ? MfgRules.Fail(MfgErrors.Int001) : null;   // the Warehouse stops answering after the stock was checked

        var pending = await c.ReportAsync(order, 10, 30);
        pending.IsPending.Should().BeTrue();
        c.F.Inspections.Should().BeEmpty();
        c.Op(order, 10).IpqcHeldQty.Should().Be(0m);

        c.Stock.Intercept = null;
        var document = c.Documents.Docs.Single(d => d.Type == StockDocumentType.Backflush);
        await c.Posting.PostAsync(document, "job");
        await c.Posting.PostAsync(document, "job");

        c.F.Inspections.Should().ContainSingle();
        c.Op(order, 10).IpqcHeldQty.Should().Be(30m);
    }

    [Fact]
    public async Task Reversing_a_report_whose_inspection_is_pending_cancels_it_and_lifts_the_hold()
    {
        var (c, order) = await ReadyAsync();
        await c.LinkIpqcPlanAsync(order, 10);
        var report = (await c.ReportAsync(order, 10, 30)).Report;

        await c.ReverseReportAsync(order, report.Id);

        c.F.Inspections.Single().Status.Should().Be(InspectionStatus.Cancelled);
        var op = c.Op(order, 10);
        (op.GoodQty, op.IpqcHeldQty).Should().Be((0m, 0m));
    }

    [Fact]
    public async Task Reversing_a_report_whose_inspection_has_a_result_is_refused()
    {
        var (c, order) = await ReadyAsync();
        await c.LinkIpqcPlanAsync(order, 10);
        var report = (await c.ReportAsync(order, 10, 30)).Report;
        var inspection = c.F.Inspections.Single();
        await c.SubmitAsync(inspection.Id, InspectionHelpers.Values(await c.GetInspectionAsync(inspection.Id)));

        var refused = await Refused(() => c.ReverseReportAsync(order, report.Id));

        refused.RuleName.Should().Be(MfgErrors.Rev003);
        refused.Message.Should().Contain(inspection.DocNo);
    }

    [Fact]
    public async Task While_output_is_held_the_next_operation_does_not_complete_on_the_part_that_passed()
    {
        var (c, order) = await ReadyAsync();
        await c.LinkIpqcPlanAsync(order, 10);
        await c.ReportAsync(order, 10, 60);
        await c.ReportAsync(order, 10, 40);
        var first = c.F.Inspections.First();
        await c.SubmitAsync(first.Id, InspectionHelpers.Values(await c.GetInspectionAsync(first.Id)));

        await c.ReportAsync(order, 20, 60);

        c.Op(order, 10).Status.Should().Be(OperationStatus.Completed);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready, "40 pieces of operation 10 still wait for their inspection");
        var second = c.F.Inspections.Last();
        await c.SubmitAsync(second.Id, InspectionHelpers.Values(await c.GetInspectionAsync(second.Id)));
        c.Op(order, 20).Status.Should().Be(OperationStatus.Ready, "60 of the 100 are taken");
        await c.ReportAsync(order, 20, 40);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Completed);
    }

    [Fact]
    public async Task The_next_operation_completes_only_on_the_passed_input()
    {
        var (c, order) = await ReadyAsync();
        await c.LinkIpqcPlanAsync(order, 10);
        await c.ReportAsync(order, 10, 100);
        var inspection = c.F.Inspections.Single();
        await c.SubmitAsync(inspection.Id, InspectionHelpers.Values(await c.GetInspectionAsync(inspection.Id)));

        await c.ReportAsync(order, 20, 100);

        c.Op(order, 10).Status.Should().Be(OperationStatus.Completed);
        c.Op(order, 20).Status.Should().Be(OperationStatus.Completed);
    }
}
