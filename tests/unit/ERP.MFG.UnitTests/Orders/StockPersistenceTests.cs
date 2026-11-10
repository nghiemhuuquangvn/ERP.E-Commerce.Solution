using ERP.MFG.Application.Abstractions;
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
using Newtonsoft.Json;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>The stock document on a database context (EF InMemory): mapping, the flows the limits are computed from, the job's cross-tenant query and the job round itself.</summary>
public class StockPersistenceTests
{
    private static MfgDbContext NewDb(Guid tenant, string name)
    {
        var tenants = new Mock<ITenantProvider>();
        tenants.SetupGet(t => t.CurrentTenantId).Returns(tenant);
        return new MfgDbContext(new DbContextOptionsBuilder<MfgDbContext>().UseInMemoryDatabase(name).Options, tenants.Object);
    }

    private static MaterialTransaction NewDocument(Guid orderId, Guid materialId, string docNo, StockDocumentType type = StockDocumentType.Issue, decimal qty = 5m, string? key = null)
        => MaterialTransaction.Create(docNo, type, Guid.NewGuid(), orderId, "MO-1", new DateOnly(2026, 11, 4), null, "note", key, "hash",
            [new NewStockLine(materialId, 10, Guid.NewGuid(), "NVL-VIT01", Guid.NewGuid(), null, qty, true, null), new NewStockLine(materialId, 10, Guid.NewGuid(), "NVL-VIT01", Guid.NewGuid(), null, 1m, false, null)], "tester");

    [Fact]
    public async Task A_document_and_its_lines_survive_a_round_trip()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "doc-roundtrip");
        var document = NewDocument(Guid.NewGuid(), Guid.NewGuid(), "MI-2611-00001", key: "k-1");
        var next = new DateTime(2026, 11, 4, 1, 5, 0, DateTimeKind.Utc);
        document.RecordTransientFailure(new string('x', 400), next);
        new StockDocumentRepository(db).Add(document);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await new StockDocumentRepository(db).FindByKeyAsync("k-1");

        loaded!.DocNo.Should().Be("MI-2611-00001");
        loaded.Status.Should().Be(StockDocumentStatus.Pending);
        loaded.Attempts.Should().Be(1);
        loaded.LastError.Should().HaveLength(400);
        loaded.NextAttemptAt.Should().Be(next);
        loaded.Lines.Select(l => (l.LineNo, l.MovementKey, l.IsFromReservation)).Should().Equal((1, "MI-2611-00001#1", true), (2, "MI-2611-00001#2F", false));
        (await new StockDocumentRepository(db).FindByKeyAsync("other")).Should().BeNull();
    }

    [Fact]
    public async Task Flows_list_only_pending_and_posted_lines_of_open_documents()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "doc-flows");
        var orderId = Guid.NewGuid();
        var material = Guid.NewGuid();

        var posted = NewDocument(orderId, material, "MI-2611-00001");
        foreach (var line in posted.Lines) posted.RecordLinePosted(line, Guid.NewGuid(), 3m, -15m, null);
        posted.MarkPosted("t", DateTime.UtcNow);
        var pending = NewDocument(orderId, material, "MI-2611-00002", StockDocumentType.Return, 2m);
        var failed = NewDocument(orderId, material, "MI-2611-00003");
        failed.RefuseLine(failed.Lines[0], "WH_X", "no");
        failed.MarkFailed("t");
        var other = NewDocument(Guid.NewGuid(), material, "MI-2611-00004");
        db.MaterialTransactions.AddRange(posted, pending, failed, other);
        await db.SaveChangesAsync();

        var flows = await new StockDocumentRepository(db).FlowsAsync(orderId);

        flows.Should().HaveCount(4);   // 2 posted lines + 2 pending lines; nothing of the failed or foreign documents
        flows.Count(f => f.DocumentStatus == StockDocumentStatus.Posted && f.UnitCost == 3m).Should().Be(2);
        flows.Count(f => f.Type == StockDocumentType.Return && f.DocumentStatus == StockDocumentStatus.Pending).Should().Be(2);
        (await new StockDocumentRepository(db).CountPendingAsync(orderId)).Should().Be(1);
    }

    [Fact]
    public async Task The_job_query_finds_due_pending_documents_in_every_tenant()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var now = new DateTime(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc);
        const string database = "doc-due";
        Guid dueA, dueB;

        await using (var dbA = NewDb(a, database))
        {
            var due = NewDocument(Guid.NewGuid(), Guid.NewGuid(), "MI-2611-00001"); due.Claim(now.AddSeconds(-5));
            var later = NewDocument(Guid.NewGuid(), Guid.NewGuid(), "MI-2611-00002"); later.Claim(now.AddMinutes(5));
            var done = NewDocument(Guid.NewGuid(), Guid.NewGuid(), "MI-2611-00003");
            foreach (var line in done.Lines) done.RecordLinePosted(line, Guid.NewGuid(), 1m, -1m, null);
            done.MarkPosted("t", now);
            dbA.MaterialTransactions.AddRange(due, later, done);
            await dbA.SaveChangesAsync();
            dueA = due.Id;
        }
        await using (var dbB = NewDb(b, database))
        {
            var due = NewDocument(Guid.NewGuid(), Guid.NewGuid(), "MI-2611-00001"); due.Claim(now.AddSeconds(-1));
            dbB.MaterialTransactions.Add(due);
            await dbB.SaveChangesAsync();
            dueB = due.Id;
        }

        await using var reader = NewDb(a, database);
        var found = await new StockDocumentRepository(reader).ListDueAsync(now, 50);

        found.Select(f => f.DocumentId).Should().BeEquivalentTo([dueA, dueB]);
        found.Single(f => f.DocumentId == dueB).TenantId.Should().Be(b);
    }

    [Fact]
    public async Task The_events_of_a_posted_document_leave_through_the_outbox_with_their_lines()
    {
        var tenant = Guid.NewGuid();
        await using var db = NewDb(tenant, "doc-outbox");
        var document = NewDocument(Guid.NewGuid(), Guid.NewGuid(), "MI-2611-00001");
        db.MaterialTransactions.Add(document);
        await db.SaveChangesAsync();
        foreach (var line in document.Lines) document.RecordLinePosted(line, Guid.NewGuid(), 2m, -10m, null);
        document.MarkPosted("tester", DateTime.UtcNow);
        await db.SaveChangesAsync();

        var message = await db.OutboxMessages.SingleAsync(m => m.Type.Contains("MaterialIssuedEvent"));
        var evt = JsonConvert.DeserializeObject<ERP.MFG.Domain.Events.MaterialIssuedEvent>(message.Content, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })!;

        evt.TenantId.Should().Be(tenant);
        evt.DocNo.Should().Be("MI-2611-00001");
        evt.Lines.Should().HaveCount(2);
        evt.Lines.Sum(l => l.Quantity).Should().Be(6m);
    }

    [Fact]
    public async Task The_job_round_claims_each_document_and_posts_it_inside_its_tenant_scope()
    {
        var tenant = Guid.NewGuid();
        var document = NewDocument(Guid.NewGuid(), Guid.NewGuid(), "MI-2611-00001");
        Guid? seenTenant = null;
        DateTime? claimedUntil = null;

        var repo = new Mock<IStockDocumentRepository>();
        repo.Setup(r => r.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([(tenant, document.Id)]);
        repo.Setup(r => r.GetAsync(document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        var uow = new Mock<IMfgUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => claimedUntil = document.NextAttemptAt).ReturnsAsync(1);
        var posting = new Mock<IStockPostingService>();
        posting.Setup(p => p.PostAsync(document, "system", It.IsAny<CancellationToken>()))
            .Callback(() => seenTenant = TenantScope.Current)
            .ReturnsAsync(new StockPostingResult(StockDocumentStatus.Posted, null, null));
        var services = new ServiceCollection().AddSingleton(repo.Object).AddSingleton(uow.Object).AddSingleton(posting.Object).BuildServiceProvider();

        var done = await new StockPostingRetryJob(services, NullLogger<StockPostingRetryJob>.Instance).RunOnceAsync(default);

        done.Should().Be(1);
        seenTenant.Should().Be(tenant);
        claimedUntil.Should().BeAfter(DateTime.UtcNow.AddMinutes(1));   // claimed for the lease before it was sent
    }

    [Fact]
    public async Task A_lost_claim_leaves_the_document_to_the_other_instance()
    {
        var document = NewDocument(Guid.NewGuid(), Guid.NewGuid(), "MI-2611-00001");
        var repo = new Mock<IStockDocumentRepository>();
        repo.Setup(r => r.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([(Guid.NewGuid(), document.Id)]);
        repo.Setup(r => r.GetAsync(document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        var uow = new Mock<IMfgUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new ERP.Shared.Infrastructure.Exceptions.ConcurrencyException("claimed elsewhere"));
        var posting = new Mock<IStockPostingService>(MockBehavior.Strict);
        var services = new ServiceCollection().AddSingleton(repo.Object).AddSingleton(uow.Object).AddSingleton(posting.Object).BuildServiceProvider();

        var done = await new StockPostingRetryJob(services, NullLogger<StockPostingRetryJob>.Instance).RunOnceAsync(default);

        done.Should().Be(0);
        posting.VerifyNoOtherCalls();
    }
}
