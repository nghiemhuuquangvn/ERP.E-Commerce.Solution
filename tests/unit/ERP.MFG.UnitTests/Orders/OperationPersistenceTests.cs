using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Events;
using ERP.MFG.Infrastructure.Persistence;
using ERP.MFG.Infrastructure.Repositories;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>Time logs, operation reports and the backflush columns on a database context (EF InMemory): mapping, the repositories' queries and the outbox event of an applied report.</summary>
public class OperationPersistenceTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc);

    private static MfgDbContext NewDb(Guid tenant, string name)
    {
        var tenants = new Mock<ITenantProvider>();
        tenants.SetupGet(t => t.CurrentTenantId).Returns(tenant);
        return new MfgDbContext(new DbContextOptionsBuilder<MfgDbContext>().UseInMemoryDatabase(name).Options, tenants.Object);
    }

    private static TimeLogWorker Worker(Guid employee) => new(employee, "NV001", "Employee NV001", null, 55_000m);

    private static OperationReport Report(Guid orderId, int seq = 10, string docNo = "OR-2611-00001", string? key = null, decimal good = 60, decimal scrap = 2)
        => OperationReport.Create(docNo, Guid.NewGuid(), orderId, "MO-2611-00001", Guid.NewGuid(), seq, new DateOnly(2026, 11, 4), good, scrap, 0, scrap > 0 ? Guid.NewGuid() : null, null, null, "note", key, "hash", T0, "tester");

    [Fact]
    public async Task A_time_log_survives_a_round_trip_with_its_pause_and_duration()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "log-roundtrip");
        var employee = Guid.NewGuid();
        var log = LaborTimeLog.StartNow(Guid.NewGuid(), Guid.NewGuid(), 10, Worker(employee), T0, "tester");
        log.Pause(T0.AddHours(1));
        log.Resume(T0.AddHours(1).AddMinutes(15));
        log.Stop(T0.AddHours(2));
        new TimeLogRepository(db).Add(log);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await new TimeLogRepository(db).GetAsync(log.Id);

        loaded!.Should().Match<LaborTimeLog>(l => l.EmployeeId == employee && l.EmployeeNumber == "NV001" && l.DurationMinutes == 105m && l.PausedMinutes == 15m && l.LaborRatePerHour == 55_000m && !l.IsOpen);
        loaded.EndAt.Should().Be(T0.AddHours(2));
    }

    [Fact]
    public async Task The_overlap_query_sees_open_and_closed_logs_of_the_same_employee_only()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "log-overlap");
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var order = Guid.NewGuid();
        var op = Guid.NewGuid();
        var repo = new TimeLogRepository(db);
        var closed = LaborTimeLog.Manual(order, op, 10, Worker(a), T0, T0.AddHours(1), T0.AddHours(5), "t");
        var open = LaborTimeLog.StartNow(order, op, 10, Worker(b), T0.AddHours(2), "t");
        repo.Add(closed);
        repo.Add(open);
        await db.SaveChangesAsync();

        (await repo.HasOverlapAsync(a, T0.AddMinutes(30), T0.AddHours(2))).Should().BeTrue();
        (await repo.HasOverlapAsync(a, T0.AddHours(1), T0.AddHours(2))).Should().BeFalse();    // touching
        (await repo.HasOverlapAsync(b, T0.AddHours(9), null)).Should().BeTrue();               // the open log runs on
        (await repo.HasOverlapAsync(b, T0, T0.AddHours(1))).Should().BeFalse();                // ended before it started
        (await repo.ListOpenForOperationAsync(op)).Should().ContainSingle().Which.EmployeeId.Should().Be(b);
        (await repo.ListOpenForOrderAsync(order)).Should().HaveCount(1);
        (await repo.ListForOrderAsync(order)).Select(l => l.EmployeeId).Should().Equal(a, b);
    }

    [Fact]
    public async Task A_report_is_found_by_its_key_and_the_pending_ones_are_listed_per_order()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "report-queries");
        var order = Guid.NewGuid();
        var repo = new OperationReportRepository(db);
        var pending = Report(order, docNo: "OR-2611-00001", key: "k-1");
        pending.WaitFor(Guid.NewGuid());
        var posted = Report(order, docNo: "OR-2611-00002");
        posted.MarkPosted(T0, "t");
        var failed = Report(order, docNo: "OR-2611-00003");
        failed.MarkFailed("MFG_MAT_008", "no stock", "t");
        var other = Report(Guid.NewGuid(), docNo: "OR-2611-00004");
        foreach (var r in new[] { pending, posted, failed, other }) repo.Add(r);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await repo.FindByKeyAsync("k-1"))!.Id.Should().Be(pending.Id);
        (await repo.FindByKeyAsync("nope")).Should().BeNull();
        (await repo.ListPendingForOrderAsync(order)).Select(r => r.DocNo).Should().Equal("OR-2611-00001");
        (await repo.ListForOrderAsync(order)).Should().HaveCount(3);
        var loaded = await repo.GetAsync(failed.Id);
        (loaded!.Status, loaded.FailureCode, loaded.FailureMessage).Should().Be((OperationReportStatus.Failed, "MFG_MAT_008", "no stock"));
    }

    [Fact]
    public async Task A_backflush_document_keeps_its_report_and_its_negative_flag()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "bf-doc");
        var reportId = Guid.NewGuid();
        var material = Guid.NewGuid();
        var document = MaterialTransaction.Create("BF-2611-00001", StockDocumentType.Backflush, Guid.NewGuid(), Guid.NewGuid(), "MO-2611-00001", new DateOnly(2026, 11, 4), null, "bf", null, null,
            [new NewStockLine(material, 10, Guid.NewGuid(), "NVL-VIT01", Guid.NewGuid(), null, 900, true, null), new NewStockLine(material, 10, Guid.NewGuid(), "NVL-VIT01", Guid.NewGuid(), null, 60, false, null, IsNegativeStock: true)],
            "tester", reportId);
        new StockDocumentRepository(db).Add(document);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await new StockDocumentRepository(db).GetAsync(document.Id);

        loaded!.SourceOperationReportId.Should().Be(reportId);
        loaded.Type.Should().Be(StockDocumentType.Backflush);
        loaded.Lines.Select(l => (l.MovementKey, l.IsNegativeStock)).Should().Equal(("BF-2611-00001#1", false), ("BF-2611-00001#2F", true));
        loaded.IsConsumption.Should().BeTrue();
    }

    [Fact]
    public async Task An_applied_report_leaves_through_the_outbox_with_its_figures()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "report-outbox");
        var header = new OrderHeaderValues(Guid.NewGuid(), Guid.NewGuid(), "TP-BAN01", "Table", 100, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, new DateOnly(2026, 11, 13),
            Guid.NewGuid(), OrderType.Standard, OrderSourceType.Manual, null, null, null);
        var snapshot = new OrderSnapshot([], [new OrderOperationValues(10, "Assembly", Guid.NewGuid(), "WC-ASM", 15m, 10m, RunTimeBasis.PerUnit, null, 0, 0, false, 115m, 115)], []);
        var start = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var order = ProductionOrder.Create("MO-2611-00001", header, snapshot, new OrderScheduleValues(start, start.AddDays(2), false, new Dictionary<int, (DateTime, DateTime)> { [10] = (start, start.AddHours(2)) }), "tester");
        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync();
        order.Release(true, "tester");
        await db.SaveChangesAsync();

        var report = OperationReport.Create("OR-2611-00001", order.PlantId, order.Id, order.OrderNo, order.OperationOf(10).Id, 10, new DateOnly(2026, 11, 4), 60, 2, 0, Guid.NewGuid(), null, null, null, null, null, T0, "tester");
        order.ApplyOperationReport(report, "tester", T0);
        report.MarkPosted(T0, "tester");
        db.OperationReports.Add(report);
        await db.SaveChangesAsync();

        var message = await db.OutboxMessages.SingleAsync(m => m.Type.Contains("OperationReportedEvent"));
        var evt = JsonConvert.DeserializeObject<OperationReportedEvent>(message.Content, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })!;
        evt.TenantId.Should().Be(tenant);
        (evt.DocNo, evt.OperationSeq, evt.GoodQty, evt.ScrapQty).Should().Be(("OR-2611-00001", 10, 60m, 2m));
        order.Status.Should().Be(OrderStatus.InProgress);
        order.ScrappedQty.Should().Be(2m);
    }
}
