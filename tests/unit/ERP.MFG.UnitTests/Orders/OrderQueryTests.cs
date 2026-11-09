using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Domain.Common;
using ERP.MFG.Infrastructure.Services;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>The list, one order and its history (T3.2.4, T3.11): filters, paging, and an order of a plant the caller may not see does not exist for them.</summary>
public class OrderQueryTests
{
    private static async Task<OrderCtx> SeededAsync()
    {
        var ctx = new OrderCtx();
        await ctx.SeedAllAsync();
        return ctx;
    }

    private static GetOrdersHandler List(OrderCtx ctx, Application.Abstractions.IPlantAccessService? access = null) => new(access ?? ctx.Access, ctx.Repo);

    private static GetOrdersQuery Query(OrderCtx ctx, OrderStatus? status = null, bool? late = null, string? search = null, int page = 1, int size = 25, int? priority = null,
        DateOnly? from = null, DateOnly? to = null)
        => new(ctx.Plant.Id, status, null, from, to, late, priority, search, page, size);

    [Fact]
    public async Task The_List_Is_Ordered_By_Due_Date_Then_Priority_And_Pages()
    {
        var ctx = await SeededAsync();
        await ctx.CreateOrderAsync(due: new DateOnly(2026, 11, 20), priority: 3);
        await ctx.CreateOrderAsync(due: new DateOnly(2026, 11, 13), priority: 3);
        await ctx.CreateOrderAsync(due: new DateOnly(2026, 11, 13), priority: 1);

        var page = await List(ctx).Handle(Query(ctx, size: 2), default);

        page.Total.Should().Be(3);
        page.Items.Should().HaveCount(2);
        page.Items.Select(i => (i.DueDate, i.Priority)).Should().Equal((new DateOnly(2026, 11, 13), 1), (new DateOnly(2026, 11, 13), 3));
        (await List(ctx).Handle(Query(ctx, page: 2, size: 2), default)).Items.Should().ContainSingle().Which.DueDate.Should().Be(new DateOnly(2026, 11, 20));
    }

    [Fact]
    public async Task The_List_Filters_By_Status_Lateness_Priority_Due_Range_And_Text()
    {
        var ctx = await SeededAsync();
        var onTime = await ctx.CreateOrderAsync(due: new DateOnly(2026, 11, 13), priority: 2);
        var late = await ctx.CreateOrderAsync(due: new DateOnly(2026, 11, 4));
        ctx.Stored(onTime.Id).TransitionTo(OrderStatus.Released, "u");

        (await List(ctx).Handle(Query(ctx, status: OrderStatus.Released), default)).Items.Should().ContainSingle().Which.Id.Should().Be(onTime.Id);
        (await List(ctx).Handle(Query(ctx, late: true), default)).Items.Should().ContainSingle().Which.Id.Should().Be(late.Id);
        (await List(ctx).Handle(Query(ctx, priority: 2), default)).Items.Should().ContainSingle().Which.Id.Should().Be(onTime.Id);
        (await List(ctx).Handle(Query(ctx, from: new DateOnly(2026, 11, 10)), default)).Items.Should().ContainSingle().Which.Id.Should().Be(onTime.Id);
        (await List(ctx).Handle(Query(ctx, to: new DateOnly(2026, 11, 5)), default)).Items.Should().ContainSingle().Which.Id.Should().Be(late.Id);
        (await List(ctx).Handle(Query(ctx, search: "ban01"), default)).Total.Should().Be(2);
        (await List(ctx).Handle(Query(ctx, search: onTime.OrderNo), default)).Items.Should().ContainSingle().Which.Id.Should().Be(onTime.Id);
        (await List(ctx).Handle(Query(ctx, search: "nothing"), default)).Total.Should().Be(0);
    }

    [Fact]
    public async Task One_Order_Comes_With_Its_Lines_And_An_Empty_History()
    {
        var ctx = await SeededAsync();
        var created = await ctx.CreateOrderAsync();

        var order = await new GetOrderHandler(ctx.Access, ctx.Repo).Handle(new GetOrderQuery(created.Id), default);
        var history = await new GetOrderHistoryHandler(ctx.Access, ctx.Repo).Handle(new GetOrderHistoryQuery(created.Id), default);

        order.OrderNo.Should().Be(created.OrderNo);
        order.Materials.Should().HaveCount(7);
        order.Operations.Should().HaveCount(3);
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task The_History_Lists_Each_Change_With_Who_And_Why()
    {
        var ctx = await SeededAsync();
        var created = await ctx.CreateOrderAsync();
        var reason = Guid.NewGuid();
        ctx.Stored(created.Id).TransitionTo(OrderStatus.Released, "planner");
        ctx.Stored(created.Id).TransitionTo(OrderStatus.OnHold, "planner", reason, "waiting");

        var history = await new GetOrderHistoryHandler(ctx.Access, ctx.Repo).Handle(new GetOrderHistoryQuery(created.Id), default);

        history.Select(h => h.ToStatus).Should().Equal(OrderStatus.Released, OrderStatus.OnHold);
        history[1].Should().Match<OrderHistoryDto>(h => h.ReasonCodeId == reason && h.Note == "waiting" && h.ChangedBy == "planner");
    }

    [Fact]
    public async Task An_Unknown_Order_Is_Not_Found()
    {
        var ctx = await SeededAsync();

        var act = () => new GetOrderHandler(ctx.Access, ctx.Repo).Handle(new GetOrderQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task A_Plant_The_Caller_May_Not_See_Shows_Neither_Its_List_Nor_Its_Orders()
    {
        var ctx = await SeededAsync();
        var created = await ctx.CreateOrderAsync();
        var stranger = new PlantAccessService(ctx.F.PlantRepo, ctx.F.AccessRepo, new ERP.MFG.UnitTests.Common.FakeUser(Guid.NewGuid()));

        var list = () => List(ctx, stranger).Handle(Query(ctx), default);
        var one = () => new GetOrderHandler(stranger, ctx.Repo).Handle(new GetOrderQuery(created.Id), default);
        var history = () => new GetOrderHistoryHandler(stranger, ctx.Repo).Handle(new GetOrderHistoryQuery(created.Id), default);

        await list.Should().ThrowAsync<EntityNotFoundException>();
        await one.Should().ThrowAsync<EntityNotFoundException>();
        await history.Should().ThrowAsync<EntityNotFoundException>();
    }
}
