using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Shared.UnitTests.Data;

public class TenantIsolationTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private sealed class Note : AggregateRoot
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class FixedTenantProvider(Guid? tenantId) : ITenantProvider
    {
        public Guid? CurrentTenantId => tenantId;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantProvider tenantProvider)
        : DbContext(options), ITenantScopedDbContext
    {
        public DbSet<Note> Notes => Set<Note>();
        public Guid? CurrentTenantId => tenantProvider.CurrentTenantId;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Note>().Ignore(n => n.DomainEvents);
            modelBuilder.Entity<Note>().HasQueryFilter(n => !n.IsDeleted);
            modelBuilder.ApplyTenantQueryFilters(this);
        }

        public override int SaveChanges()
        {
            ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
            return base.SaveChanges();
        }
    }

    // One database name per test, but the SAME context type for every instance — so EF's
    // cached model is shared exactly like in a running service (the old constant-filter bug).
    private readonly string _database = Guid.NewGuid().ToString();

    private TestDbContext Context(Guid? tenantId) => new(
        new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(_database).Options,
        new FixedTenantProvider(tenantId));

    private void Seed(Guid tenantId, params string[] texts)
    {
        using var db = Context(tenantId);
        foreach (var text in texts)
            db.Notes.Add(new Note { Text = text });
        db.SaveChanges();
    }

    [Fact]
    public void Queries_OnlyReturnTheCurrentTenantsRows_ForEveryContextInstance()
    {
        Seed(TenantA, "a1", "a2");
        Seed(TenantB, "b1");

        using (var a = Context(TenantA))
            a.Notes.Select(n => n.Text).Should().BeEquivalentTo("a1", "a2");

        // Second tenant on the same cached model must not inherit tenant A's filter.
        using (var b = Context(TenantB))
            b.Notes.Select(n => n.Text).Should().BeEquivalentTo("b1");
    }

    [Fact]
    public void Queries_WithNoTenant_ReturnNothing()
    {
        Seed(TenantA, "a1");

        using var anonymous = Context(null);

        anonymous.Notes.Should().BeEmpty();
    }

    [Fact]
    public void Filter_ComposesWithExistingEntityFilter()
    {
        Seed(TenantA, "kept");
        using (var db = Context(TenantA))
        {
            var deleted = new Note { Text = "deleted" };
            typeof(BaseEntity).GetProperty(nameof(BaseEntity.IsDeleted))!.SetValue(deleted, true);
            db.Notes.Add(deleted);
            db.SaveChanges();
        }

        using var read = Context(TenantA);
        read.Notes.Select(n => n.Text).Should().BeEquivalentTo("kept");
    }

    [Fact]
    public void Save_StampsCurrentTenantOnNewRows()
    {
        using var db = Context(TenantA);
        var note = new Note { Text = "x" };
        db.Notes.Add(note);

        db.SaveChanges();

        note.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void Save_RowExplicitlyOwnedByAnotherTenant_Throws()
    {
        using var db = Context(TenantA);
        db.Notes.Add(new Note { Text = "x", TenantId = TenantB });

        var act = () => db.SaveChanges();

        act.Should().Throw<TenantIsolationViolationException>();
    }

    [Fact]
    public void Save_WithNoTenant_Throws()
    {
        using var db = Context(null);
        db.Notes.Add(new Note { Text = "x" });

        var act = () => db.SaveChanges();

        act.Should().Throw<TenantIsolationViolationException>();
    }

    [Fact]
    public void TenantProvider_ExplicitScopeWinsOverCaller_AndRestoresOnDispose()
    {
        var provider = new TenantProvider(() => TenantA.ToString());

        provider.CurrentTenantId.Should().Be(TenantA);
        using (TenantScope.Begin(TenantB))
            provider.CurrentTenantId.Should().Be(TenantB);
        provider.CurrentTenantId.Should().Be(TenantA);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void TenantProvider_UnknownCallerTenant_IsNull(string? callerTenant)
    {
        new TenantProvider(() => callerTenant).CurrentTenantId.Should().BeNull();
    }
}
