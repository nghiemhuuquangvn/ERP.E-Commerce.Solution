using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.BackgroundJobs;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Security;
using ERP.Shared.Authorization.TokenRevocation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Security;

/// <summary>S4a / SEC-M8 (docs/business/Identity/TokenRevocation.md): a committed change never loses its revocation.</summary>
public class RevocationOutboxTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AuthDbContext NewDb() => new(
        new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(_dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options,
        new Mock<IDomainEventDispatcher>().Object);

    /// <summary>The unit of work only needs its context and the queue here; every repository argument is left null.</summary>
    private static UnitOfWork NewUnitOfWork(AuthDbContext db, ITokenRevocationQueue queue)
    {
        var ctor = typeof(UnitOfWork).GetConstructors().Single();
        var args = ctor.GetParameters().Select(p =>
            p.ParameterType == typeof(AuthDbContext) ? db
            : p.ParameterType == typeof(ITokenRevocationQueue) ? (object)queue
            : null).ToArray();
        return (UnitOfWork)ctor.Invoke(args);
    }

    private static Mock<ITokenRevocationStore> DownStore()
    {
        var store = new Mock<ITokenRevocationStore>();
        store.Setup(s => s.MarkUsersChangedAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), It.IsAny<long?>()))
            .ThrowsAsync(new TimeoutException("redis down"));
        return store;
    }

    [Fact]
    public async Task ARevocation_IsSavedWithTheChange_AndPublishedRightAfterTheCommit()
    {
        var store = new InMemoryTokenRevocationStore();
        await using var db = NewDb();
        var queue = new TokenRevocationQueue(store, NullLogger<TokenRevocationQueue>.Instance);
        var uow = NewUnitOfWork(db, queue);
        var user = Guid.NewGuid();

        uow.Revocations.UsersChanged(user);
        await uow.SaveChangesAsync(CancellationToken.None);

        var row = await NewDb().RevocationOutbox.SingleAsync();
        row.Kind.Should().Be(RevocationKind.Users);
        row.Subject.Should().Be(user.ToString());
        row.PublishedAt.Should().NotBeNull();
        (await store.GetStateAsync(null, user.ToString(), null)).UserChangedAt.Should().Be(row.ChangedAtUnix);
    }

    [Fact]
    public async Task WhenTheStoreIsDown_TheRowStays_AndThePublisherDeliversItLater()
    {
        await using (var db = NewDb())
        {
            var uow = NewUnitOfWork(db, new TokenRevocationQueue(DownStore().Object, NullLogger<TokenRevocationQueue>.Instance));
            uow.Revocations.UsersChanged(Guid.NewGuid());
            await uow.SaveChangesAsync(CancellationToken.None); // the change commits although Redis is down
        }

        var pending = await NewDb().RevocationOutbox.SingleAsync();
        pending.PublishedAt.Should().BeNull("never dropped");
        pending.Attempts.Should().Be(1);

        var store = new InMemoryTokenRevocationStore();
        var services = new ServiceCollection();
        services.AddScoped(_ => NewDb());
        services.AddScoped<ITokenRevocationQueue>(_ => new TokenRevocationQueue(store, NullLogger<TokenRevocationQueue>.Instance));
        var publisher = new RevocationOutboxPublisher(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RevocationOutboxPublisher>.Instance);

        (await publisher.PublishPendingAsync(CancellationToken.None)).Should().Be(1);

        var published = await NewDb().RevocationOutbox.SingleAsync();
        published.PublishedAt.Should().NotBeNull();
        (await store.GetStateAsync(null, published.Subject, null)).UserChangedAt.Should().Be(published.ChangedAtUnix,
            "the marker is the time of the change, not of the late publish");
    }

    [Fact]
    public async Task ARolledBackChange_PublishesNothing()
    {
        var store = new Mock<ITokenRevocationStore>();
        await using var db = NewDb();
        var uow = NewUnitOfWork(db, new TokenRevocationQueue(store.Object, NullLogger<TokenRevocationQueue>.Instance));

        await uow.BeginTransactionAsync(CancellationToken.None);
        uow.Revocations.UsersChanged(Guid.NewGuid());
        await uow.SaveChangesAsync(CancellationToken.None); // inside the transaction: staged, not published
        await uow.RollbackTransactionAsync(CancellationToken.None);

        store.Verify(s => s.MarkUsersChangedAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), It.IsAny<long?>()), Times.Never);
    }

    [Fact]
    public async Task AMarker_IsNeverLowered_ByALateRetryOfAnOlderChange()
    {
        var store = new InMemoryTokenRevocationStore();
        var user = Guid.NewGuid();

        await store.MarkUsersChangedAsync([user], changedAtUnix: 2000);
        await store.MarkUsersChangedAsync([user], changedAtUnix: 1000);

        (await store.GetStateAsync(null, user.ToString(), null)).UserChangedAt.Should().Be(2000);
    }
}
