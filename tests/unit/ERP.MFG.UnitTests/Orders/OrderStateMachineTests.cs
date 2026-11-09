using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Events;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>BR-PO-02 / BA spec 01 §4.2 and §4.3: every pair of states, the history and the events (TC-PO-039..042, TC-PO-022).</summary>
public class OrderStateMachineTests
{
    private static readonly (OrderStatus From, OrderStatus To)[] Valid =
    [
        (OrderStatus.Draft, OrderStatus.Released), (OrderStatus.Draft, OrderStatus.Cancelled),
        (OrderStatus.Released, OrderStatus.Draft), (OrderStatus.Released, OrderStatus.Cancelled), (OrderStatus.Released, OrderStatus.InProgress), (OrderStatus.Released, OrderStatus.OnHold),
        (OrderStatus.InProgress, OrderStatus.OnHold), (OrderStatus.InProgress, OrderStatus.Completed),
        (OrderStatus.OnHold, OrderStatus.Released), (OrderStatus.OnHold, OrderStatus.InProgress),
        (OrderStatus.Completed, OrderStatus.InProgress), (OrderStatus.Completed, OrderStatus.Closed),
    ];

    public static IEnumerable<object[]> AllPairs()
    {
        foreach (var from in Enum.GetValues<OrderStatus>())
            foreach (var to in Enum.GetValues<OrderStatus>())
                yield return [from, to, Valid.Contains((from, to))];
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void TC_PO_041_Only_The_Pairs_Of_The_State_Diagram_Are_Allowed(OrderStatus from, OrderStatus to, bool allowed)
    {
        OrderStateMachine.CanTransition(from, to).Should().Be(allowed);

        var act = () => OrderStateMachine.EnsureTransition(from, to);
        if (allowed) act.Should().NotThrow();
        else act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(from == OrderStatus.Closed ? MfgErrors.Po014 : MfgErrors.Po002);
    }

    [Fact]
    public void The_Message_Of_A_Refused_Transition_Names_Both_Statuses()
    {
        var act = () => OrderStateMachine.EnsureTransition(OrderStatus.Released, OrderStatus.Closed);

        act.Should().Throw<BusinessRuleException>().Which.Message.Should().Contain("Released").And.Contain("Closed");
    }

    public static IEnumerable<object[]> AllOperationPairs()
    {
        var valid = new[]
        {
            (OperationStatus.Pending, OperationStatus.Ready), (OperationStatus.Ready, OperationStatus.Running), (OperationStatus.Ready, OperationStatus.Completed),
            (OperationStatus.Running, OperationStatus.Paused), (OperationStatus.Running, OperationStatus.Ready), (OperationStatus.Running, OperationStatus.Completed),
            (OperationStatus.Paused, OperationStatus.Running), (OperationStatus.Completed, OperationStatus.Ready),
        };
        foreach (var from in Enum.GetValues<OperationStatus>())
            foreach (var to in Enum.GetValues<OperationStatus>())
                yield return [from, to, valid.Contains((from, to))];
    }

    [Theory]
    [MemberData(nameof(AllOperationPairs))]
    public void Only_The_Pairs_Of_The_Operation_Diagram_Are_Allowed(OperationStatus from, OperationStatus to, bool allowed)
    {
        OperationStateMachine.CanTransition(from, to).Should().Be(allowed);

        var act = () => OperationStateMachine.EnsureTransition(from, to);
        if (allowed) act.Should().NotThrow();
        else act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Po002);
    }

    // ── on the aggregate ────────────────────────────────────────────────────────────────────────────────────

    private static async Task<(OrderCtx Ctx, ProductionOrder Order)> NewOrderAsync()
    {
        var ctx = new OrderCtx();
        await ctx.SeedAllAsync();
        var dto = await ctx.CreateOrderAsync();
        return (ctx, ctx.Stored(dto.Id));
    }

    [Fact]
    public async Task TC_PO_042_Every_Change_Of_Status_Is_Written_To_The_History_In_Order()
    {
        var (_, order) = await NewOrderAsync();
        var reason = Guid.NewGuid();

        order.TransitionTo(OrderStatus.Released, "planner");
        order.TransitionTo(OrderStatus.InProgress, "keeper");
        order.TransitionTo(OrderStatus.OnHold, "planner", reason, "HLD-01");
        order.TransitionTo(OrderStatus.InProgress, "planner");
        order.TransitionTo(OrderStatus.Completed, "keeper");
        order.TransitionTo(OrderStatus.Closed, "accountant");

        order.History.Select(h => (h.FromStatus, h.ToStatus)).Should().Equal(
            (OrderStatus.Draft, OrderStatus.Released), (OrderStatus.Released, OrderStatus.InProgress), (OrderStatus.InProgress, OrderStatus.OnHold),
            (OrderStatus.OnHold, OrderStatus.InProgress), (OrderStatus.InProgress, OrderStatus.Completed), (OrderStatus.Completed, OrderStatus.Closed));
        order.History.Select(h => h.ChangedBy).Should().Equal("planner", "keeper", "planner", "planner", "keeper", "accountant");
        var hold = order.History.Single(h => h.ToStatus == OrderStatus.OnHold);
        hold.ReasonCodeId.Should().Be(reason);
        hold.Note.Should().Be("HLD-01");
        order.ActualStart.Should().NotBeNull();
        order.ActualEnd.Should().NotBeNull();
    }

    [Fact]
    public async Task TC_PO_022_Resume_Goes_Back_To_The_Status_Before_The_Hold_And_Only_There()
    {
        var (_, order) = await NewOrderAsync();
        order.TransitionTo(OrderStatus.Released, "u");
        order.TransitionTo(OrderStatus.InProgress, "u");
        order.TransitionTo(OrderStatus.OnHold, "u");
        order.StatusBeforeHold.Should().Be(OrderStatus.InProgress);

        var wrong = () => order.TransitionTo(OrderStatus.Released, "u");
        wrong.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Po002);

        order.TransitionTo(OrderStatus.InProgress, "u");
        order.Status.Should().Be(OrderStatus.InProgress);
        order.StatusBeforeHold.Should().BeNull();
    }

    [Fact]
    public async Task A_Released_Order_That_Is_Held_Resumes_As_Released()
    {
        var (_, order) = await NewOrderAsync();
        order.TransitionTo(OrderStatus.Released, "u");
        order.TransitionTo(OrderStatus.OnHold, "u");

        var wrong = () => order.TransitionTo(OrderStatus.InProgress, "u");
        wrong.Should().Throw<BusinessRuleException>();

        order.TransitionTo(OrderStatus.Released, "u");
        order.Status.Should().Be(OrderStatus.Released);
    }

    [Fact]
    public async Task TC_PO_039_A_Closed_Order_Cannot_Be_Changed()
    {
        var (ctx, order) = await NewOrderAsync();
        order.TransitionTo(OrderStatus.Released, "u");
        order.TransitionTo(OrderStatus.InProgress, "u");
        order.TransitionTo(OrderStatus.Completed, "u");
        order.TransitionTo(OrderStatus.Closed, "u");

        order.Invoking(o => o.EnsureEditable()).Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Po014);
        order.Invoking(o => o.TransitionTo(OrderStatus.InProgress, "u")).Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Po014);

        var update = () => ctx.UpdateOrderAsync(order.Id, 90, new DateOnly(2026, 11, 13));
        (await update.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po014);
    }

    [Fact]
    public async Task A_Cancelled_Order_Is_Final()
    {
        var (_, order) = await NewOrderAsync();
        order.TransitionTo(OrderStatus.Cancelled, "u");

        order.Invoking(o => o.TransitionTo(OrderStatus.Draft, "u")).Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Po002);
        order.Invoking(o => o.EnsureEditable()).Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Po002);
    }

    [Fact]
    public async Task TC_PO_040_Closing_A_Released_Order_Is_Refused()
    {
        var (_, order) = await NewOrderAsync();
        order.TransitionTo(OrderStatus.Released, "u");

        order.Invoking(o => o.TransitionTo(OrderStatus.Closed, "u")).Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be(MfgErrors.Po002);
        order.Status.Should().Be(OrderStatus.Released);
        order.History.Should().ContainSingle();
    }

    [Fact]
    public async Task The_Created_And_Status_Changed_Events_Are_Raised_For_The_Outbox()
    {
        var (_, order) = await NewOrderAsync();
        order.TransitionTo(OrderStatus.Released, "planner");

        order.DomainEvents.OfType<ProductionOrderCreatedEvent>().Should().ContainSingle().Which.OrderNo.Should().Be(order.OrderNo);
        var changed = order.DomainEvents.OfType<ProductionOrderStatusChangedEvent>().Should().ContainSingle().Subject;
        (changed.FromStatus, changed.ToStatus, changed.ChangedBy).Should().Be((OrderStatus.Draft, OrderStatus.Released, "planner"));
    }
}
