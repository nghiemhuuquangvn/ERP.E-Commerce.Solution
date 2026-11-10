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

/// <summary>Receipt lines, the lot queries, the settled quantity and the completion / close events on a database context (EF InMemory).</summary>
public class ReceiptPersistenceTests
{
    private static MfgDbContext NewDb(Guid tenant, string name)
    {
        var tenants = new Mock<ITenantProvider>();
        tenants.SetupGet(t => t.CurrentTenantId).Returns(tenant);
        return new MfgDbContext(new DbContextOptionsBuilder<MfgDbContext>().UseInMemoryDatabase(name).Options, tenants.Object);
    }

    private static MaterialTransaction Receipt(Guid orderId, Guid itemId, string docNo, string lot, bool hold = true)
        => MaterialTransaction.Create(docNo, StockDocumentType.Receipt, Guid.NewGuid(), orderId, "MO-2611-00001", new DateOnly(2026, 11, 4), null, null, null, null,
            [NewStockLine.Receipt(itemId, "TP-BAN01", Guid.NewGuid(), 100, null, null, lot, new DateTime(2027, 5, 3, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc), hold)], "tester");

    [Fact]
    public async Task A_receipt_line_survives_a_round_trip_without_a_material()
    {
        await using var db = NewDb(Guid.NewGuid(), "fr-roundtrip");
        var document = Receipt(Guid.NewGuid(), Guid.NewGuid(), "FR-2611-00001", "TP-BAN01-261104-01");
        new StockDocumentRepository(db).Add(document);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await new StockDocumentRepository(db).GetAsync(document.Id);

        var line = loaded!.Lines.Single();
        (line.Kind, line.OrderMaterialId, line.LotNumber, line.IsQcHold, line.MovementKey).Should().Be((StockLineKind.Receipt, null, "TP-BAN01-261104-01", true, "FR-2611-00001#1"));
        line.ExpiryDate.Should().Be(new DateTime(2027, 5, 3, 0, 0, 0, DateTimeKind.Utc));
        loaded.ReceiptLines.Should().HaveCount(1);
        loaded.MaterialLines.Should().BeEmpty();
    }

    [Fact]
    public async Task Lot_numbers_in_use_and_held_lots_are_found_per_item_and_order()
    {
        await using var db = NewDb(Guid.NewGuid(), "fr-lots");
        var order = Guid.NewGuid();
        var item = Guid.NewGuid();
        var repo = new StockDocumentRepository(db);
        var held = Receipt(order, item, "FR-2611-00001", "LOT-A");
        foreach (var l in held.Lines) held.RecordLinePosted(l, Guid.NewGuid(), 5m, 500m, Guid.NewGuid());
        held.MarkPosted("t", DateTime.UtcNow);
        var released = Receipt(order, item, "FR-2611-00002", "LOT-B");
        foreach (var l in released.Lines) released.RecordLinePosted(l, Guid.NewGuid(), 5m, 500m, Guid.NewGuid());
        released.MarkPosted("t", DateTime.UtcNow);
        released.RecordQcReleased(released.Lines.Single(), DateTime.UtcNow);
        var pending = Receipt(order, item, "FR-2611-00003", "LOT-C");
        var failed = Receipt(order, item, "FR-2611-00004", "LOT-D");
        failed.RefuseLine(failed.Lines.Single(), "WH_X", "no");
        failed.MarkFailed("t");
        var other = Receipt(Guid.NewGuid(), Guid.NewGuid(), "FR-2611-00005", "LOT-E");
        foreach (var d in new[] { held, released, pending, failed, other }) repo.Add(d);
        await db.SaveChangesAsync();

        (await repo.LotNumbersInUseAsync(item)).Should().BeEquivalentTo("LOT-A", "LOT-B", "LOT-C");   // not the failed one, not another item's
        (await repo.CountHeldLotsAsync(order)).Should().Be(1);                                            // LOT-A: posted, on hold, not released
    }

    [Fact]
    public async Task Completing_and_closing_leave_events_and_keep_the_settled_quantity()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "fr-events");
        var header = new OrderHeaderValues(Guid.NewGuid(), Guid.NewGuid(), "TP-BAN01", "Table", 100, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, new DateOnly(2026, 11, 13),
            Guid.NewGuid(), OrderType.Standard, OrderSourceType.Manual, null, null, null);
        var snapshot = new OrderSnapshot([new OrderMaterialValues(10, Guid.NewGuid(), "NVL-SON01", "Paint", Guid.NewGuid(), 20, 52.5m, 5m, IssueMethod.Manual, Guid.NewGuid(), null)],
            [new OrderOperationValues(10, "Assembly", Guid.NewGuid(), "WC-ASM", 15m, 10m, RunTimeBasis.PerUnit, null, 0, 0, false, 115m, 115)], []);
        var start = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var order = ProductionOrder.Create("MO-2611-00001", header, snapshot, new OrderScheduleValues(start, start.AddDays(2), false, new Dictionary<int, (DateTime, DateTime)> { [10] = (start, start.AddHours(2)) }), "tester");
        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync();
        order.Release(true, "tester");
        order.TransitionTo(OrderStatus.InProgress, "tester");
        var material = order.Materials.Single();
        var document = MaterialTransaction.Create("MI-2611-00001", StockDocumentType.Issue, order.PlantId, order.Id, order.OrderNo, new DateOnly(2026, 11, 4), null, null, null, null,
            [new NewStockLine(material.Id, material.LineNo, material.ComponentItemId, "NVL-SON01", Guid.NewGuid(), null, 52.5m, true, null)], "tester");
        foreach (var l in document.Lines) document.RecordLinePosted(l, Guid.NewGuid(), 2m, -105m, null);
        order.ApplyPostedDocument(document, "tester");
        order.Complete("tester", start, 2m, canShortClose: true, reasonIsShortClose: true, Guid.NewGuid(), "short", 0);
        await db.SaveChangesAsync();
        order.Close("tester", 0, 0, 0, confirmConsumeRemaining: true);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.ProductionOrders.Include(o => o.AllMaterials).SingleAsync();
        (reloaded.Status, reloaded.AllMaterials.Single().SettledIssueQty, reloaded.AllMaterials.Single().ConsumedQty).Should().Be((OrderStatus.Closed, 52.5m, 52.5m));
        var completed = JsonConvert.DeserializeObject<ProductionOrderCompletedEvent>((await db.OutboxMessages.SingleAsync(m => m.Type.Contains("ProductionOrderCompletedEvent"))).Content,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })!;
        (completed.TenantId, completed.ShortClosed, completed.Automatic).Should().Be((tenant, true, false));
        var closed = JsonConvert.DeserializeObject<ProductionOrderClosedEvent>((await db.OutboxMessages.SingleAsync(m => m.Type.Contains("ProductionOrderClosedEvent"))).Content,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })!;
        closed.UnsettledMaterialCarriedToConsumption.Should().BeTrue();
    }

    [Fact]
    public async Task A_posted_receipt_raises_the_received_event_with_its_lot_lines()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "fr-received");
        var document = Receipt(Guid.NewGuid(), Guid.NewGuid(), "FR-2611-00001", "TP-BAN01-261104-01");
        db.MaterialTransactions.Add(document);
        await db.SaveChangesAsync();
        foreach (var l in document.Lines) document.RecordLinePosted(l, Guid.NewGuid(), 5m, 500m, Guid.NewGuid());
        document.MarkPosted("tester", DateTime.UtcNow);
        await db.SaveChangesAsync();

        var message = await db.OutboxMessages.SingleAsync(m => m.Type.Contains("FinishedGoodsReceivedEvent"));
        var evt = JsonConvert.DeserializeObject<FinishedGoodsReceivedEvent>(message.Content, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })!;

        evt.TenantId.Should().Be(tenant);
        evt.Lines.Should().ContainSingle().Which.Should().Match<ReceiptMovementLine>(l => l.LotNumber == "TP-BAN01-261104-01" && l.IsQcHold && l.Quantity == 100m);
    }
}
