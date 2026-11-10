using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Events;
using ERP.MFG.Infrastructure.Persistence;
using ERP.MFG.Infrastructure.Repositories;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>The parts that need a database context (EF InMemory): the open-order guard and the outbox writer.</summary>
public class OrderPersistenceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid PlantId = Guid.NewGuid();
    private static readonly Guid WorkCenterA = Guid.NewGuid();
    private static readonly Guid WorkCenterB = Guid.NewGuid();

    private static MfgDbContext NewDb(Guid tenant, string name)
    {
        var tenants = new Mock<ITenantProvider>();
        tenants.SetupGet(t => t.CurrentTenantId).Returns(tenant);
        return new MfgDbContext(new DbContextOptionsBuilder<MfgDbContext>().UseInMemoryDatabase(name).Options, tenants.Object);
    }

    private static ProductionOrder NewOrder(string number, Guid workCenter, int quantity = 10)
    {
        var header = new OrderHeaderValues(PlantId, Guid.NewGuid(), "TP-BAN01", "Table", quantity, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, new DateOnly(2026, 11, 13),
            Guid.NewGuid(), OrderType.Standard, OrderSourceType.Manual, null, null, null);
        var snapshot = new OrderSnapshot(
            [new OrderMaterialValues(10, Guid.NewGuid(), "NVL-VIT01", "Screw", Guid.NewGuid(), 10, 160m, 0m, IssueMethod.Backflush, Guid.NewGuid(), null)],
            [new OrderOperationValues(10, "Assembly", workCenter, "WC-ASM", 15m, 10m, RunTimeBasis.PerUnit, null, 0, 0, false, 115m, 115)],
            []);
        var start = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        return ProductionOrder.Create(number, header, snapshot, new OrderScheduleValues(start, start.AddDays(2), false, new Dictionary<int, (DateTime, DateTime)> { [10] = (start, start.AddHours(2)) }), "tester");
    }

    private static async Task<MfgDbContext> SeededAsync(string name, params (string No, OrderStatus Status, Guid WorkCenter)[] orders)
    {
        var db = NewDb(Tenant, name);
        foreach (var (no, status, workCenter) in orders)
        {
            var order = NewOrder(no, workCenter);
            db.ProductionOrders.Add(order);
            await db.SaveChangesAsync();
            foreach (var step in PathTo(status)) order.TransitionTo(step, "tester");
            await db.SaveChangesAsync();
        }
        return db;
    }

    private static IEnumerable<OrderStatus> PathTo(OrderStatus status) => status switch
    {
        OrderStatus.Draft => [],
        OrderStatus.Released => [OrderStatus.Released],
        OrderStatus.InProgress => [OrderStatus.Released, OrderStatus.InProgress],
        OrderStatus.OnHold => [OrderStatus.Released, OrderStatus.OnHold],
        OrderStatus.Completed => [OrderStatus.Released, OrderStatus.InProgress, OrderStatus.Completed],
        OrderStatus.Closed => [OrderStatus.Released, OrderStatus.InProgress, OrderStatus.Completed, OrderStatus.Closed],
        OrderStatus.Cancelled => [OrderStatus.Cancelled],
        _ => [],
    };

    // ── open-order guard ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OrderStatus.Released, true)]
    [InlineData(OrderStatus.InProgress, true)]
    [InlineData(OrderStatus.OnHold, true)]
    [InlineData(OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Draft, false)]
    [InlineData(OrderStatus.Closed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public async Task The_Guard_Counts_An_Order_As_Open_Only_Between_Release_And_Close(OrderStatus status, bool open)
    {
        await using var db = await SeededAsync("guard-orders-" + status, ("MO-1", status, WorkCenterA));

        (await new OrderBackedOpenOrderGuard(db).HasOpenOrdersAsync(PlantId)).Should().Be(open);
    }

    [Theory] // R219: unfinished = anything that is not Closed or Cancelled, a Draft included
    [InlineData(OrderStatus.Draft, 1)]
    [InlineData(OrderStatus.Released, 1)]
    [InlineData(OrderStatus.InProgress, 1)]
    [InlineData(OrderStatus.OnHold, 1)]
    [InlineData(OrderStatus.Completed, 1)]
    [InlineData(OrderStatus.Closed, 0)]
    [InlineData(OrderStatus.Cancelled, 0)]
    public async Task R219_The_Guard_Counts_Every_Order_That_Is_Not_Closed_Or_Cancelled(OrderStatus status, int expected)
    {
        await using var db = await SeededAsync("unfinished-" + status, ("MO-7", status, WorkCenterA));
        var guard = new OrderBackedOpenOrderGuard(db);

        var (count, example) = await guard.GetUnfinishedOrdersAsync(PlantId);

        count.Should().Be(expected);
        example.Should().Be(expected == 0 ? null : "MO-7");
        (await guard.GetUnfinishedOrdersAsync(Guid.NewGuid())).Count.Should().Be(0);   // another plant
    }

    [Fact]
    public async Task The_Guard_Does_Not_Look_At_Another_Plant()
    {
        await using var db = await SeededAsync("guard-plant", ("MO-1", OrderStatus.Released, WorkCenterA));

        (await new OrderBackedOpenOrderGuard(db).HasOpenOrdersAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.Released, true)]
    [InlineData(OrderStatus.InProgress, true)]
    [InlineData(OrderStatus.OnHold, true)]
    [InlineData(OrderStatus.Draft, false)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Closed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public async Task The_Guard_Blocks_A_Work_Center_Only_While_An_Operation_Of_A_Running_Order_Is_Not_Done(OrderStatus status, bool busy)
    {
        await using var db = await SeededAsync("guard-wc-" + status, ("MO-1", status, WorkCenterA));
        var guard = new OrderBackedOpenOrderGuard(db);

        (await guard.HasOpenOperationsAsync(WorkCenterA)).Should().Be(busy);
        (await guard.HasOpenOperationsAsync(WorkCenterB)).Should().BeFalse("another work center is not in use");
    }

    [Fact]
    public async Task A_Finished_Operation_Frees_The_Work_Center()
    {
        await using var db = await SeededAsync("guard-op-done", ("MO-1", OrderStatus.Released, WorkCenterA));
        var operation = await db.ProductionOrderOperations.SingleAsync();
        operation.TransitionTo(OperationStatus.Ready);
        operation.TransitionTo(OperationStatus.Completed);
        await db.SaveChangesAsync();

        (await new OrderBackedOpenOrderGuard(db).HasOpenOperationsAsync(WorkCenterA)).Should().BeFalse();
    }

    [Fact]
    public async Task A_Work_Center_Used_By_An_Order_Cannot_Be_Deleted_And_A_Used_Reason_Is_Reported()
    {
        await using var db = await SeededAsync("references", ("MO-1", OrderStatus.Draft, WorkCenterA));
        var order = await db.ProductionOrders.SingleAsync();
        var reason = Guid.NewGuid();
        order.TransitionTo(OrderStatus.Cancelled, "tester", reason, "not needed");
        await db.SaveChangesAsync();
        var references = new ReferenceChecker(db);

        (await references.WorkCenterIsUsedAsync(WorkCenterA)).Should().BeTrue();
        (await references.WorkCenterIsUsedAsync(WorkCenterB)).Should().BeFalse();
        (await references.ReasonIsUsedAsync(reason)).Should().BeTrue();
        (await references.ReasonIsUsedAsync(Guid.NewGuid())).Should().BeFalse();
    }

    // ── outbox ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_A_New_Order_Writes_One_Outbox_Message_Stamped_With_The_Tenant()
    {
        await using var db = NewDb(Tenant, "outbox-new");
        var order = NewOrder("MO-2611-00001", WorkCenterA);
        db.ProductionOrders.Add(order);

        await db.SaveChangesAsync();

        var message = await db.OutboxMessages.SingleAsync();
        message.ProcessedOnUtc.Should().BeNull();
        message.Type.Should().Contain(nameof(ProductionOrderCreatedEvent));
        order.DomainEvents.Should().BeEmpty("the events left with the transaction");
        OutboxEventDeserializer.TryDeserialize(message, out var restored, out var error).Should().BeTrue(error);
        var created = restored.Should().BeOfType<ProductionOrderCreatedEvent>().Subject;
        created.TenantId.Should().Be(Tenant);
        created.OrderNo.Should().Be("MO-2611-00001");
        created.OrderId.Should().Be(order.Id);
        EventTenant.TryRead(message.Content, out var tenantInPayload).Should().BeTrue();
        tenantInPayload.Should().Be(Tenant);
    }

    [Fact]
    public async Task Every_Status_Change_Adds_A_Message_And_A_Save_Without_Events_Adds_None()
    {
        await using var db = NewDb(Tenant, "outbox-status");
        var order = NewOrder("MO-1", WorkCenterA);
        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync();

        order.TransitionTo(OrderStatus.Released, "planner");
        await db.SaveChangesAsync();
        await db.SaveChangesAsync();   // nothing changed

        var messages = await db.OutboxMessages.OrderBy(m => m.OccurredOnUtc).ToListAsync();
        messages.Should().HaveCount(2);
        messages.Select(m => m.Type).Should().Contain(t => t.Contains(nameof(ProductionOrderStatusChangedEvent)));
        var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(messages.Single(m => m.Type.Contains(nameof(ProductionOrderStatusChangedEvent))).Content)!;
        json["ChangedBy"].Should().Be("planner");
    }

    [Fact]
    public async Task The_Event_Id_Survives_The_Outbox()
    {
        await using var db = NewDb(Tenant, "outbox-eventid");
        var order = NewOrder("MO-1", WorkCenterA);
        var original = order.DomainEvents.OfType<ProductionOrderCreatedEvent>().Single().EventId;
        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync();

        OutboxEventDeserializer.TryDeserialize(await db.OutboxMessages.SingleAsync(), out var restored, out _);

        ((ProductionOrderCreatedEvent)restored!).EventId.Should().Be(original);
    }

    // ── tenant isolation and the repository ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_Tenant_Sees_Nothing_Of_The_Orders()
    {
        await using (var mine = NewDb(Tenant, "tenants"))
        {
            mine.ProductionOrders.Add(NewOrder("MO-1", WorkCenterA));
            await mine.SaveChangesAsync();
        }

        await using var other = NewDb(Guid.NewGuid(), "tenants");
        (await new ProductionOrderRepository(other).ListAsync(new OrderFilter(PlantId, null, null, null, null, null, null, null), 1, 25)).Total.Should().Be(0);
        await using var again = NewDb(Tenant, "tenants");
        (await new ProductionOrderRepository(again).ListAsync(new OrderFilter(PlantId, null, null, null, null, null, null, null), 1, 25)).Total.Should().Be(1);
    }

    [Fact]
    public async Task The_Repository_Loads_The_Order_With_Its_Live_Lines_And_Filters_The_List()
    {
        await using var db = NewDb(Tenant, "repo");
        var late = NewOrder("MO-LATE", WorkCenterA);
        db.ProductionOrders.Add(late);
        db.ProductionOrders.Add(NewOrder("MO-OK", WorkCenterA));
        await db.SaveChangesAsync();
        var repo = new ProductionOrderRepository(db);

        var loaded = await repo.GetAsync(late.Id);

        loaded!.Materials.Should().ContainSingle();
        loaded.Operations.Should().ContainSingle();
        (await repo.ListAsync(new OrderFilter(PlantId, null, null, null, null, null, null, "late"), 1, 25)).Items.Should().ContainSingle().Which.OrderNo.Should().Be("MO-LATE");
        (await repo.ListAsync(new OrderFilter(PlantId, OrderStatus.Released, null, null, null, null, null, null), 1, 25)).Total.Should().Be(0);
        (await repo.ListAsync(new OrderFilter(PlantId, null, null, null, null, null, null, null), 1, 1)).Items.Should().HaveCount(1);
    }
}
