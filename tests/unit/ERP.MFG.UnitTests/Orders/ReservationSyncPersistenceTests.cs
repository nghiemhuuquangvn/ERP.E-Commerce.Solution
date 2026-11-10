using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.BackgroundJobs;
using ERP.MFG.Infrastructure.Persistence;
using ERP.MFG.Infrastructure.Repositories;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>The sync flag on the stored order and the cross-tenant query the reservation job starts from (EF InMemory).</summary>
public class ReservationSyncPersistenceTests
{
    private static MfgDbContext NewDb(Guid tenant, string name)
    {
        var tenants = new Mock<ITenantProvider>();
        tenants.SetupGet(t => t.CurrentTenantId).Returns(tenant);
        return new MfgDbContext(new DbContextOptionsBuilder<MfgDbContext>().UseInMemoryDatabase(name).Options, tenants.Object);
    }

    private static ProductionOrder NewOrder(string number)
    {
        var header = new OrderHeaderValues(Guid.NewGuid(), Guid.NewGuid(), "TP-BAN01", "Table", 10, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, new DateOnly(2026, 11, 13),
            Guid.NewGuid(), OrderType.Standard, OrderSourceType.Manual, null, null, null);
        var snapshot = new OrderSnapshot(
            [new OrderMaterialValues(10, Guid.NewGuid(), "NVL-VIT01", "Screw", Guid.NewGuid(), 10, 160m, 0m, IssueMethod.Backflush, Guid.NewGuid(), null)],
            [new OrderOperationValues(10, "Assembly", Guid.NewGuid(), "WC-ASM", 15m, 10m, RunTimeBasis.PerUnit, null, 0, 0, false, 115m, 115)], []);
        var start = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        return ProductionOrder.Create(number, header, snapshot, new OrderScheduleValues(start, start.AddDays(2), false, new Dictionary<int, (DateTime, DateTime)> { [10] = (start, start.AddHours(2)) }), "tester");
    }

    [Fact]
    public async Task The_sync_columns_survive_a_round_trip()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "sync-roundtrip");
        var order = NewOrder("MO-1");
        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync();
        order.Release(true, "tester");
        var next = new DateTime(2026, 11, 4, 1, 5, 0, DateTimeKind.Utc);
        order.RecordReservationSyncFailure(new string('x', 400), next);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await new ProductionOrderRepository(db).GetAsync(order.Id);

        loaded!.ReservationSyncPending.Should().BeTrue();
        loaded.ReservationSyncAttempts.Should().Be(1);
        loaded.ReservationSyncError.Should().HaveLength(400);
        loaded.ReservationNextAttemptAt.Should().Be(next);
    }

    [Fact]
    public async Task The_job_query_finds_only_due_flagged_orders_in_every_tenant()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var now = new DateTime(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc);
        const string database = "sync-query";

        Guid dueA, dueB;
        await using (var dbA = NewDb(a, database))
        {
            var due = NewOrder("MO-A1"); var later = NewOrder("MO-A2"); var plain = NewOrder("MO-A3");
            dbA.ProductionOrders.AddRange(due, later, plain);
            await dbA.SaveChangesAsync();
            due.Release(true, "t"); later.Release(true, "t");
            later.RecordReservationSyncFailure("down", now.AddMinutes(5));
            await dbA.SaveChangesAsync();
            dueA = due.Id;
        }
        await using (var dbB = NewDb(b, database))
        {
            var due = NewOrder("MO-B1");
            dbB.ProductionOrders.Add(due);
            await dbB.SaveChangesAsync();
            due.Release(true, "t");
            await dbB.SaveChangesAsync();
            dueB = due.Id;
        }

        await using var reader = NewDb(a, database);
        var found = await new ProductionOrderRepository(reader).ListPendingReservationSyncAsync(now, 50);

        found.Select(f => f.OrderId).Should().BeEquivalentTo([dueA, dueB]);
        found.Single(f => f.OrderId == dueB).TenantId.Should().Be(b);
    }

    [Fact]
    public async Task The_job_round_with_nothing_pending_does_nothing_and_does_not_throw()
    {
        var repo = new Mock<ERP.MFG.Application.Abstractions.IProductionOrderRepository>();
        repo.Setup(r => r.ListPendingReservationSyncAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var services = new ServiceCollection().AddSingleton(repo.Object).BuildServiceProvider();

        var done = await new ReservationSyncJob(services, NullLogger<ReservationSyncJob>.Instance).RunOnceAsync(default);

        done.Should().Be(0);
    }

    [Fact]
    public async Task The_job_round_syncs_each_pending_order_inside_its_own_tenant_scope()
    {
        var tenant = Guid.NewGuid();
        var order = NewOrder("MO-J1");
        order.Release(true, "t");
        Guid? seenTenant = null;

        var repo = new Mock<ERP.MFG.Application.Abstractions.IProductionOrderRepository>();
        repo.Setup(r => r.ListPendingReservationSyncAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([(tenant, order.Id)]);
        repo.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var sync = new Mock<ERP.MFG.Application.Abstractions.IOrderReservationSync>();
        sync.Setup(s => s.SyncAsync(order, It.IsAny<CancellationToken>()))
            .Callback(() => seenTenant = TenantScope.Current)
            .ReturnsAsync(new ERP.MFG.Application.Abstractions.ReservationSyncResult(true, null));
        var services = new ServiceCollection().AddSingleton(repo.Object).AddSingleton(sync.Object).BuildServiceProvider();

        var done = await new ReservationSyncJob(services, NullLogger<ReservationSyncJob>.Instance).RunOnceAsync(default);

        done.Should().Be(1);
        seenTenant.Should().Be(tenant);
    }
}
