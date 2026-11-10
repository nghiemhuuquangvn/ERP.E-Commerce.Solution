using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

public class ReservationPlannerTests
{
    private static ReservationNeed Need(decimal need, decimal allocatable, decimal own = 0) => new(1, Guid.NewGuid(), "X", need, allocatable, own);

    [Theory]
    [InlineData(400, 300, 0, 300, 100)]     // TC-PO-011: needs 400, the Warehouse has 300
    [InlineData(52.5, 100, 0, 52.5, 0)]
    [InlineData(63, 33, 30, 63, 0)]         // what the order already holds counts as available to it
    [InlineData(10, 0, 0, 0, 10)]
    [InlineData(0, 50, 0, 0, 0)]
    public void Target_is_the_smaller_of_need_and_what_can_be_held(decimal need, decimal allocatable, decimal own, decimal target, decimal shortage)
    {
        var plan = ReservationPlanner.Plan(Need(need, allocatable, own));
        plan.Target.Should().Be(target);
        plan.Shortage.Should().Be(shortage);
    }

    [Fact]
    public void Open_need_is_required_minus_net_issue_and_consumption_and_never_negative()
    {
        ReservationPlanner.OpenNeed(63, 30, 0, 0).Should().Be(33);
        ReservationPlanner.OpenNeed(63, 30, 5, 0).Should().Be(38);
        ReservationPlanner.OpenNeed(63, 70, 0, 0).Should().Be(0);
        ReservationPlanner.OpenNeed(63, 30, 0, 33).Should().Be(0);
    }
}

public class ReleaseOrderTests
{
    [Fact] // TC-PO-011
    public async Task Availability_lists_only_the_short_material()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();

        var result = await c.AvailabilityAsync(order.Id);

        result.HasShortage.Should().BeTrue();
        var short1 = result.Materials.Where(m => m.Shortage > 0).Should().ContainSingle().Subject;
        short1.ComponentCode.Should().Be("BTP-CHAN01");
        (short1.Need, short1.Allocatable, short1.Shortage).Should().Be((400m, 300m, 100m));
    }

    [Fact]
    public async Task Expired_and_held_lots_are_not_allocatable()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        var son = c.Items["NVL-SON01"].Id;
        c.Stock.Balances.Remove(son);
        c.Stock.Lot(son, c.RawWarehouse, "A", 30);
        c.Stock.Lot(son, c.RawWarehouse, "B", 22.5m);
        c.Stock.Lot(son, c.RawWarehouse, "X", 20, expiry: new DateTime(2026, 10, 1));
        c.Stock.Lot(son, c.RawWarehouse, "Q", 15, status: "QcHold");

        var result = await c.AvailabilityAsync(order.Id);

        result.Materials.Single(m => m.ComponentCode == "NVL-SON01").Allocatable.Should().Be(52.5m);
    }

    [Fact] // TC-PO-012
    public async Task Release_with_a_shortage_is_allowed_and_reserves_the_smaller_quantity()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();

        var result = await c.ReleaseAsync(order.Id);

        result.Order.Status.Should().Be(OrderStatus.Released);
        result.Shortages.Should().ContainSingle(s => s.ComponentCode == "BTP-CHAN01");
        result.ReservationPending.Should().BeFalse();
        var stored = c.Stored(order.Id);
        stored.ReservationSyncPending.Should().BeFalse();
        c.Stock.HeldFor(order.Id, c.Items["BTP-CHAN01"].Id).Should().Be(300m);
        c.Stock.HeldFor(order.Id, c.Items["NVL-VIT01"].Id).Should().Be(1600m);
        stored.Materials.Single(m => m.ComponentCode == "BTP-CHAN01").ReservedQty.Should().Be(300m);
        stored.ReservationId.Should().NotBeNull();
    }

    [Fact]
    public async Task Release_makes_the_first_operation_ready_and_keeps_the_others_pending()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();

        await c.ReleaseAsync(order.Id);

        c.Stored(order.Id).Operations.Select(o => o.Status).Should().Equal(OperationStatus.Ready, OperationStatus.Pending, OperationStatus.Pending);
    }

    [Fact] // TC-PO-015
    public async Task Release_without_sequential_operations_makes_every_operation_ready()
    {
        var c = new ReleaseCtx();
        c.PlantSettings.Sequential = false;
        var order = await c.DraftAsync();

        await c.ReleaseAsync(order.Id);

        c.Stored(order.Id).Operations.Should().OnlyContain(o => o.Status == OperationStatus.Ready);
    }

    [Fact] // TC-PO-013
    public async Task A_shortage_blocks_release_when_the_plant_says_so()
    {
        var c = new ReleaseCtx();
        c.PlantSettings.BlockReleaseOnShortage = true;
        var order = await c.DraftAsync();

        var act = () => c.ReleaseAsync(order.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po007);
        c.Stored(order.Id).Status.Should().Be(OrderStatus.Draft);
        c.Stock.Reserves.Should().Be(0);
    }

    [Fact] // TC-PO-014
    public async Task An_inactive_component_blocks_release()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        c.Replace("NVL-VIT01", p => p with { IsActive = false });

        var act = () => c.ReleaseAsync(order.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po006);
        c.Stored(order.Id).Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task An_inactive_work_center_blocks_release()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        c.Centers["WC-PNT"].SetActive(false, hasOpenOperations: false);

        var act = () => c.ReleaseAsync(order.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po006);
    }

    [Fact]
    public async Task A_serial_controlled_component_blocks_release()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        c.Replace("NVL-XOP01", p => p with { IsSerialControlled = true });

        var act = () => c.ReleaseAsync(order.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat011);
    }

    [Fact]
    public async Task Only_a_draft_can_be_released()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);

        var act = () => c.ReleaseAsync(order.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po002);
    }

    [Fact]
    public async Task Release_raises_its_event_and_a_status_change()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();

        await c.ReleaseAsync(order.Id);

        c.Stored(order.Id).History.Last().ToStatus.Should().Be(OrderStatus.Released);
    }

    [Fact]
    public async Task An_unreachable_warehouse_fails_the_release_closed()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        c.Stock.Down = true;

        var act = () => c.ReleaseAsync(order.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Int001);
        c.Stored(order.Id).Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task A_warehouse_that_goes_down_after_the_check_leaves_a_pending_reservation_and_the_job_step_finishes_it()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        c.Stock.RefuseReserveWith = "WH_INSUFFICIENT_STOCK";

        var result = await c.ReleaseAsync(order.Id);

        result.Order.Status.Should().Be(OrderStatus.Released);
        result.ReservationPending.Should().BeTrue();
        var stored = c.Stored(order.Id);
        stored.ReservationSyncPending.Should().BeTrue();
        stored.ReservationSyncError.Should().Contain("refused");
        stored.ReservationSyncAttempts.Should().Be(1);
        stored.ReservationNextAttemptAt.Should().BeAfter(c.Clock.Now.UtcDateTime);

        c.Stock.RefuseReserveWith = null;
        (await c.Sync.SyncAsync(stored)).InSync.Should().BeTrue();
        stored.ReservationSyncPending.Should().BeFalse();
        c.Stock.HeldFor(order.Id, c.Items["BTP-CHAN01"].Id).Should().Be(300m);
    }

    [Fact]
    public async Task Syncing_twice_changes_nothing_in_the_warehouse()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);
        var reserves = c.Stock.Reserves;

        var result = await c.Sync.SyncAsync(c.Stored(order.Id));

        result.InSync.Should().BeTrue();
        c.Stock.Reserves.Should().Be(reserves);
        c.Stock.Releases.Should().Be(0);
    }

    [Fact]
    public async Task When_more_stock_arrives_the_next_sync_reserves_more()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);
        var chan = c.Items["BTP-CHAN01"].Id;
        c.Stock.Stock(chan, c.RawWarehouse, 200);

        await c.Sync.SyncAsync(c.Stored(order.Id));

        c.Stock.HeldFor(order.Id, chan).Should().Be(400m);
    }
}

public class OrderTransitionTests
{
    [Fact] // TC-PO-016
    public async Task Unrelease_returns_a_draft_and_frees_the_reservation()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);

        var result = await c.UnreleaseAsync(order.Id);

        result.Order.Status.Should().Be(OrderStatus.Draft);
        c.Stored(order.Id).Operations.Should().OnlyContain(o => o.Status == OperationStatus.Pending);
        c.Stock.HeldFor(order.Id, c.Items["NVL-VIT01"].Id).Should().Be(0m);
        c.Stored(order.Id).Materials.Should().OnlyContain(m => m.ReservedQty == 0);
        c.Stored(order.Id).ReservationSyncPending.Should().BeFalse();
    }

    [Fact] // TC-PO-017
    public async Task Unrelease_is_refused_after_a_material_was_issued()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);
        Touch.Issue(c.Stored(order.Id), "NVL-VIT01", 10);

        var act = () => c.UnreleaseAsync(order.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po008);
        c.Stored(order.Id).Status.Should().Be(OrderStatus.Released);
    }

    [Fact] // TC-PO-026
    public async Task Cancel_a_draft_with_a_reason()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();

        var result = await c.CancelAsync(order.Id, c.CancelReason.Id);

        result.Order.Status.Should().Be(OrderStatus.Cancelled);
        c.Stored(order.Id).History.Last().ReasonCodeId.Should().Be(c.CancelReason.Id);
        c.Stock.Reserves.Should().Be(0);
    }

    [Fact]
    public async Task Cancel_needs_a_reason_of_the_right_type()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();

        foreach (var reason in new Guid?[] { null, Guid.Empty, Guid.NewGuid(), c.HoldReason.Id })
        {
            var act = () => c.CancelAsync(order.Id, reason);
            (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po009);
        }
        c.Stored(order.Id).Status.Should().Be(OrderStatus.Draft);
    }

    [Fact] // TC-PO-028
    public async Task Cancel_a_released_order_frees_the_reservation_and_is_final()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);

        var result = await c.CancelAsync(order.Id, c.CancelReason.Id);

        result.Order.Status.Should().Be(OrderStatus.Cancelled);
        c.Stock.HeldFor(order.Id, c.Items["NVL-VIT01"].Id).Should().Be(0m);
        var again = () => c.ReleaseAsync(order.Id);
        (await again.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po002);
    }

    [Fact] // TC-PO-027
    public async Task An_order_in_progress_cannot_be_cancelled()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);
        c.Stored(order.Id).TransitionTo(OrderStatus.InProgress, "t");

        var act = () => c.CancelAsync(order.Id, c.CancelReason.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po002);
    }

    [Fact]
    public async Task Cancel_with_the_warehouse_down_keeps_the_flag_until_the_reservation_is_released()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);
        c.Stock.Down = true;

        var result = await c.CancelAsync(order.Id, c.CancelReason.Id);

        result.Order.Status.Should().Be(OrderStatus.Cancelled);
        result.ReservationPending.Should().BeTrue();
        c.Stock.Down = false;
        (await c.Sync.SyncAsync(c.Stored(order.Id))).InSync.Should().BeTrue();
        c.Stock.HeldFor(order.Id, c.Items["NVL-VIT01"].Id).Should().Be(0m);
        c.Stored(order.Id).ReservationSyncPending.Should().BeFalse();
    }

    [Fact] // TC-PO-019
    public async Task Hold_needs_a_hold_reason_and_keeps_the_reservation()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);

        var none = () => c.HoldAsync(order.Id, null);
        (await none.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po009);
        var wrong = () => c.HoldAsync(order.Id, c.CancelReason.Id);
        (await wrong.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po009);

        var result = await c.HoldAsync(order.Id, c.HoldReason.Id);

        result.Order.Status.Should().Be(OrderStatus.OnHold);
        result.Order.StatusBeforeHold.Should().Be(OrderStatus.Released);
        c.Stock.HeldFor(order.Id, c.Items["NVL-VIT01"].Id).Should().Be(1600m);
    }

    [Fact] // TC-PO-022
    public async Task Resume_goes_back_to_the_status_before_the_hold()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();
        await c.ReleaseAsync(order.Id);
        c.Stored(order.Id).TransitionTo(OrderStatus.InProgress, "t");
        await c.HoldAsync(order.Id, c.HoldReason.Id);

        var result = await c.ResumeAsync(order.Id);

        result.Order.Status.Should().Be(OrderStatus.InProgress);
        result.Order.StatusBeforeHold.Should().BeNull();
    }

    [Fact]
    public async Task A_draft_cannot_be_held_and_a_running_order_cannot_be_resumed()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync();

        var hold = () => c.HoldAsync(order.Id, c.HoldReason.Id);
        (await hold.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po002);
        var resume = () => c.ResumeAsync(order.Id);
        (await resume.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po002);
    }
}

public class ChangeQuantityTests
{
    [Fact] // TC-PO-024
    public async Task A_new_quantity_recomputes_the_lines_keeps_the_issue_and_reserves_the_rest()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync(100);
        await c.ReleaseAsync(order.Id);
        var stored = c.Stored(order.Id);
        stored.TransitionTo(OrderStatus.InProgress, "t");
        Touch.Issue(stored, "NVL-SON01", 30);
        var son = c.Items["NVL-SON01"].Id;

        var result = await c.ChangeQuantityAsync(order.Id, 120);

        result.Order.Quantity.Should().Be(120);
        var line = stored.Materials.Single(m => m.ComponentCode == "NVL-SON01");
        line.RequiredQty.Should().Be(63.00m);
        line.IssuedQty.Should().Be(30m);
        line.ReservedQty.Should().Be(33m);
        c.Stock.HeldFor(order.Id, son).Should().Be(33m);
        stored.Materials.Single(m => m.ComponentCode == "BTP-CHAN01").RequiredQty.Should().Be(480m);
        stored.History.Last().Note.Should().Contain("120");
        stored.ReservationSyncPending.Should().BeFalse();
    }

    [Fact]
    public async Task The_schedule_and_operation_minutes_follow_the_quantity()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync(100);
        await c.ReleaseAsync(order.Id);
        var before = c.Stored(order.Id).Operations.First().StandardMinutes;

        await c.ChangeQuantityAsync(order.Id, 120);

        c.Stored(order.Id).Operations.First().StandardMinutes.Should().BeGreaterThan(before);
    }

    [Fact] // TC-PO-023
    public async Task A_quantity_below_what_is_completed_is_refused()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync(100);
        await c.ReleaseAsync(order.Id);
        var stored = c.Stored(order.Id);
        stored.TransitionTo(OrderStatus.InProgress, "t");
        Touch.Complete(stored, 60);

        var act = () => c.ChangeQuantityAsync(order.Id, 50);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po010);
        stored.Quantity.Should().Be(100);
    }

    [Fact]
    public async Task A_held_order_can_change_quantity_and_a_draft_cannot()
    {
        var c = new ReleaseCtx();
        var draft = await c.DraftAsync(100);
        var onDraft = () => c.ChangeQuantityAsync(draft.Id, 120);
        await onDraft.Should().ThrowAsync<BusinessRuleException>();

        await c.ReleaseAsync(draft.Id);
        await c.HoldAsync(draft.Id, c.HoldReason.Id);
        var result = await c.ChangeQuantityAsync(draft.Id, 120);

        result.Order.Quantity.Should().Be(120);
        result.Order.Status.Should().Be(OrderStatus.OnHold);
    }

    [Fact]
    public async Task Zero_or_too_many_decimals_are_refused()
    {
        var c = new ReleaseCtx();
        var order = await c.DraftAsync(100);
        await c.ReleaseAsync(order.Id);

        foreach (var quantity in new[] { 0m, -5m, 10.5m })
        {
            var act = () => c.ChangeQuantityAsync(order.Id, quantity);
            await act.Should().ThrowAsync<BusinessRuleException>();
        }
    }
}

/// <summary>The shop floor figures are written by later milestones; tests set them directly.</summary>
internal static class Touch
{
    public static void Issue(ProductionOrder order, string code, decimal quantity)
    {
        var line = order.Materials.Single(m => m.ComponentCode == code);
        typeof(ProductionOrderMaterial).GetProperty(nameof(ProductionOrderMaterial.IssuedQty))!.SetValue(line, quantity);
    }

    public static void Complete(ProductionOrder order, decimal quantity)
        => typeof(ProductionOrder).GetProperty(nameof(ProductionOrder.CompletedQty))!.SetValue(order, quantity);
}
