using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Persistence;
using ERP.MFG.Infrastructure.Repositories;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ERP.MFG.UnitTests.Quality;

/// <summary>The non-conformance report on a database context (EF InMemory): mapping, the queries the handlers rely on and the rework order without a BOM.</summary>
public class NcrPersistenceTests
{
    private static MfgDbContext NewDb(Guid tenant, string name)
    {
        var tenants = new Mock<ITenantProvider>();
        tenants.SetupGet(t => t.CurrentTenantId).Returns(tenant);
        return new MfgDbContext(new DbContextOptionsBuilder<MfgDbContext>().UseInMemoryDatabase(name).Options, tenants.Object);
    }

    private static NonConformance NewNcr(string no, Guid orderId, decimal qty = 10m)
    {
        var report = OperationReport.Create("OR-2611-00001", Guid.NewGuid(), orderId, "MO-2611-00001", Guid.NewGuid(), 10, new DateOnly(2026, 11, 17), 60m, 2m, 0m, Guid.NewGuid(), null, null, null, null, null, DateTime.UtcNow, "t");
        return NonConformance.CreateManual(no, report, "MO-2611-00001", Guid.NewGuid(), "TP-BAN01", "Table", qty, "Scratched", "tester");
    }

    [Fact]
    public async Task An_ncr_and_its_dispositions_survive_a_round_trip_and_replaced_lines_stay_hidden()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "ncr-roundtrip");
        var ncr = NewNcr("NCR-2611-00001", Guid.NewGuid());
        ncr.SetDispositions([new(DispositionType.Rework, 4m, null, "again"), new(DispositionType.Scrap, 6m, Guid.NewGuid(), null)], true, "qm", DateTime.UtcNow);
        ncr.SetDispositions([new(DispositionType.UseAsIs, 10m, null, null)], true, "qm", DateTime.UtcNow);
        new NcrRepository(db).Add(ncr);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await new NcrRepository(db).GetAsync(ncr.Id);

        loaded!.NcrNo.Should().Be("NCR-2611-00001");
        (loaded.Status, loaded.NonconformingQty, loaded.Description, loaded.IsRecordOnly).Should().Be((NcrStatus.InDisposition, 10m, "Scratched", true));
        loaded.Dispositions.Should().ContainSingle().Which.Type.Should().Be(DispositionType.UseAsIs);
        loaded.AllDispositions.Should().HaveCount(3, "the replaced lines stay as deleted rows");
    }

    [Fact]
    public async Task The_queries_find_an_ncr_by_its_inspection_its_rework_order_and_the_open_count_of_an_order()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "ncr-queries");
        var orderId = Guid.NewGuid();
        var open = NewNcr("NCR-2611-00001", orderId);
        var closed = NewNcr("NCR-2611-00002", orderId, 2m);
        closed.SetDispositions([new(DispositionType.UseAsIs, 2m, null, null)], true, "qm", DateTime.UtcNow);
        closed.MarkExecuted(1, "k", null, null, "qm", DateTime.UtcNow);
        open.SetDispositions([new(DispositionType.Rework, 10m, null, null)], true, "qm", DateTime.UtcNow);
        var rework = Guid.NewGuid();
        open.MarkReworkStarted(1, rework, "qm");
        var repo = new NcrRepository(db);
        repo.Add(open);
        repo.Add(closed);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await repo.CountOpenForOrderAsync(orderId)).Should().Be(1);
        (await repo.GetByReworkOrderAsync(rework))!.NcrNo.Should().Be("NCR-2611-00001");
        (await repo.GetByReworkOrderAsync(Guid.NewGuid())).Should().BeNull();
        var (items, total) = await repo.ListAsync(new NcrFilter(open.PlantId, null, null, orderId, null, OpenOnly: true), 1, 25);
        (total, items.Single().NcrNo).Should().Be((1, "NCR-2611-00001"));
        (await repo.ListForOrderAsync(orderId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Another_tenant_sees_none_of_the_reports()
    {
        var mine = Guid.NewGuid();
        var name = "ncr-tenants";
        await using (var db = NewDb(mine, name))
        {
            var ncr = NewNcr("NCR-2611-00001", Guid.NewGuid());
            new NcrRepository(db).Add(ncr);
            await db.SaveChangesAsync();
        }
        await using var other = NewDb(Guid.NewGuid(), name);

        (await other.NonConformances.CountAsync()).Should().Be(0);
    }
}
