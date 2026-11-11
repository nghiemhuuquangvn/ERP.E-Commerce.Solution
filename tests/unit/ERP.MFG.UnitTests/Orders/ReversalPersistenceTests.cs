using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
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

/// <summary>The reversal columns, the lots kept on a line and the queries around them on a database context (EF InMemory).</summary>
public class ReversalPersistenceTests
{
    private static MfgDbContext NewDb(Guid tenant, string name)
    {
        var tenants = new Mock<ITenantProvider>();
        tenants.SetupGet(t => t.CurrentTenantId).Returns(tenant);
        return new MfgDbContext(new DbContextOptionsBuilder<MfgDbContext>().UseInMemoryDatabase(name).Options, tenants.Object);
    }

    private static readonly DateOnly Day = new(2026, 11, 4);

    private static MaterialTransaction Backflush(Guid orderId, string docNo, params NewStockLine[] lines)
        => MaterialTransaction.Create(docNo, StockDocumentType.Backflush, Guid.NewGuid(), orderId, "MO-2611-00001", Day, null, null, null, null, lines, "t");

    private static NewStockLine Out(decimal qty) => new(Guid.NewGuid(), 1, Guid.NewGuid(), "NVL-SON01", Guid.NewGuid(), null, qty, true, null);

    [Fact]
    public async Task The_lots_of_a_line_and_the_reversal_columns_survive_a_round_trip_in_their_order()
    {
        await using var db = NewDb(Guid.NewGuid(), "rv-roundtrip");
        var order = Guid.NewGuid();
        var lotA = Guid.NewGuid();
        var lotB = Guid.NewGuid();
        var original = Backflush(order, "BF-2611-00001", Out(52.5m));
        original.RecordLinePosted(original.Lines.Single(), Guid.NewGuid(), 4m, 210m, null,
            [new LineLot(lotB, "L-B", 22.5m, 4m, null, null), new LineLot(lotA, "L-A", 30m, 4m, null, lotA)]);
        original.MarkPosted("t", DateTime.UtcNow);
        var reversal = MaterialTransaction.Create("RV-2611-00001", StockDocumentType.Reversal, original.PlantId, order, "MO-2611-00001", Day, null, "wrong", null, null,
            ReversalPlanner.ReturnMaterial(original), "t", Guid.NewGuid(), original.Id);
        var repo = new StockDocumentRepository(db);
        repo.Add(original);
        repo.Add(reversal);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await new StockDocumentRepository(db).GetAsync(original.Id);
        var loadedReversal = await new StockDocumentRepository(db).GetAsync(reversal.Id);

        loaded!.Lines.Single().Lots.Select(l => (l.LotNumber, l.Qty, l.UnitCost, l.ParentLotId)).Should().Equal(("L-B", 22.5m, (decimal?)4m, (Guid?)null), ("L-A", 30m, 4m, lotA));
        (loadedReversal!.Type, loadedReversal.ReversalOfId, loadedReversal.IsReversal, loadedReversal.SourceOperationReportId).Should().Be((StockDocumentType.Reversal, original.Id, true, loadedReversal.SourceOperationReportId));
        loadedReversal.Lines.Select(l => (l.ReversalAction, l.LotId, l.Qty, l.MovementKey)).Should().Equal(
            ((ReversalAction?)ReversalAction.Return, (Guid?)lotB, 22.5m, "RV-2611-00001#1"), (ReversalAction.Return, lotA, 30m, "RV-2611-00001#2"));
        loadedReversal.Lines.Should().OnlyContain(l => l.ReversesLineId == original.Lines.Single().Id);
    }

    [Fact]
    public async Task A_document_marks_itself_reversed_and_the_queries_find_a_live_reversal_only()
    {
        await using var db = NewDb(Guid.NewGuid(), "rv-queries");
        var order = Guid.NewGuid();
        var repo = new StockDocumentRepository(db);
        var original = Backflush(order, "BF-2611-00001", Out(5m));
        original.RecordLinePosted(original.Lines.Single(), Guid.NewGuid(), 2m, 10m, null);
        original.MarkPosted("t", DateTime.UtcNow);
        var failed = MaterialTransaction.Create("RV-2611-00001", StockDocumentType.Reversal, original.PlantId, order, "MO", Day, null, "n", null, null, ReversalPlanner.ReturnMaterial(original), "t", null, original.Id);
        failed.RefuseLine(failed.Lines.Single(), "WH_X", "no");
        failed.MarkFailed("t");
        repo.Add(original);
        repo.Add(failed);
        await db.SaveChangesAsync();
        (await repo.FindReversalOfAsync(original.Id)).Should().BeNull("a failed reversal does not count");
        (await repo.CountPendingReversalsAsync(order)).Should().Be(0);

        var pending = MaterialTransaction.Create("RV-2611-00002", StockDocumentType.Reversal, original.PlantId, order, "MO", Day, null, "n", null, null, ReversalPlanner.ReturnMaterial(original), "t", null, original.Id);
        repo.Add(pending);
        await db.SaveChangesAsync();

        (await repo.FindReversalOfAsync(original.Id))!.DocNo.Should().Be("RV-2611-00002");
        (await repo.CountPendingReversalsAsync(order)).Should().Be(1);
        (await repo.CountPendingAsync(order)).Should().Be(1, "a pending reversal also blocks unrelease, cancel, complete and close");
        original.MarkReversed("t");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        (await repo.GetAsync(original.Id))!.IsReversed.Should().BeTrue();
    }

    [Fact]
    public async Task A_reversed_receipt_is_not_a_held_lot_and_pending_reports_exclude_reversals()
    {
        await using var db = NewDb(Guid.NewGuid(), "rv-held");
        var order = Guid.NewGuid();
        var item = Guid.NewGuid();
        var docs = new StockDocumentRepository(db);
        var receipt = MaterialTransaction.Create("FR-2611-00001", StockDocumentType.Receipt, Guid.NewGuid(), order, "MO", Day, null, null, null, null,
            [NewStockLine.Receipt(item, "TP-BAN01", Guid.NewGuid(), 40, null, null, "LOT-A", null, null, true)], "t");
        receipt.RecordLinePosted(receipt.Lines.Single(), Guid.NewGuid(), 5m, 200m, Guid.NewGuid());
        receipt.MarkPosted("t", DateTime.UtcNow);
        docs.Add(receipt);
        await db.SaveChangesAsync();
        (await docs.CountHeldLotsAsync(order)).Should().Be(1);

        receipt.MarkReversed("t");
        await db.SaveChangesAsync();
        (await docs.CountHeldLotsAsync(order)).Should().Be(0);

        var reports = new OperationReportRepository(db);
        var report = OperationReport.Create("OR-2611-00001", Guid.NewGuid(), order, "MO", Guid.NewGuid(), 10, Day, 5, 0, 0, null, null, null, null, null, null, DateTime.UtcNow, "t");
        report.MarkPosted(DateTime.UtcNow, "t");
        var reversal = OperationReport.CreateReversal(report, "OR-2611-00002", Day, "wrong", null, null, DateTime.UtcNow, "t");
        var normalPending = OperationReport.Create("OR-2611-00003", Guid.NewGuid(), order, "MO", Guid.NewGuid(), 10, Day, 1, 0, 0, null, null, null, null, null, null, DateTime.UtcNow, "t");
        foreach (var r in new[] { report, reversal, normalPending }) reports.Add(r);
        await db.SaveChangesAsync();

        (await reports.ListPendingForOrderAsync(order)).Select(r => r.DocNo).Should().Equal("OR-2611-00003");
        (await reports.ListPendingReversalsForOrderAsync(order)).Select(r => r.DocNo).Should().Equal("OR-2611-00002");
        (await reports.FindReversalOfAsync(report.Id))!.DocNo.Should().Be("OR-2611-00002");
        reversal.IsReversal.Should().BeTrue();
        reversal.Status.Should().Be(OperationReportStatus.Pending);
    }

    [Fact]
    public async Task The_reversal_events_leave_through_the_outbox_with_the_tenant()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "rv-events");
        var order = Guid.NewGuid();
        var receipt = MaterialTransaction.Create("FR-2611-00001", StockDocumentType.Receipt, Guid.NewGuid(), order, "MO-2611-00001", Day, null, null, null, null,
            [NewStockLine.Receipt(Guid.NewGuid(), "TP-BAN01", Guid.NewGuid(), 40, null, null, "LOT-A", null, null, true), Out(8m)], "t");
        foreach (var line in receipt.Lines) receipt.RecordLinePosted(line, Guid.NewGuid(), 5m, line.Qty * 5m, Guid.NewGuid());
        receipt.MarkPosted("t", DateTime.UtcNow);
        var reversal = MaterialTransaction.Create("RV-2611-00001", StockDocumentType.Reversal, receipt.PlantId, order, "MO-2611-00001", Day, null, "wrong", null, null,
            ReversalPlanner.TakeOutReceipt(receipt).Concat(ReversalPlanner.ReturnMaterial(receipt)).ToList(), "t", null, receipt.Id);
        foreach (var line in reversal.Lines) reversal.RecordLinePosted(line, Guid.NewGuid(), 5m, line.Qty * 5m, line.LotId);
        reversal.MarkPosted("t", DateTime.UtcNow);
        new StockDocumentRepository(db).Add(receipt);
        new StockDocumentRepository(db).Add(reversal);

        await db.SaveChangesAsync();

        var messages = await db.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        messages.Should().Contain(m => m.Type.Contains("FinishedGoodsReceiptReversedEvent")).And.Contain(m => m.Type.Contains("MaterialReturnedEvent"));
        var json = messages.Single(m => m.Type.Contains("FinishedGoodsReceiptReversedEvent")).Content;
        json.Should().Contain(tenant.ToString()).And.Contain(receipt.Id.ToString()).And.Contain("LOT-A");
        var parsed = JsonConvert.DeserializeObject<FinishedGoodsReceiptReversedEvent>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        parsed!.Lines.Should().ContainSingle().Which.Quantity.Should().Be(40m);
    }
}
