using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Shared.UnitTests.Data;

/// <summary>Tenant isolation for non-BaseEntity POCOs, platform-wide rows and shared reference data.</summary>
public class TenantOwnershipTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid Platform = WellKnownTenants.Platform;

    /// <summary>Plain POCO with a private setter, like WareHouseService's entities.</summary>
    private sealed class Bin : ITenantOwned
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; private set; }
        public string Code { get; set; } = string.Empty;
    }

    private sealed class SystemSettings : AggregateRoot, IPlatformWideEntity
    {
        public bool Flag { get; set; }
    }

    private sealed class Country : AggregateRoot, ISharedReferenceData
    {
        public string Code { get; set; } = string.Empty;
    }

    private sealed class FixedTenantProvider(Guid? tenantId) : ITenantProvider
    {
        public Guid? CurrentTenantId => tenantId;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantProvider tenantProvider)
        : DbContext(options), ITenantScopedDbContext
    {
        public DbSet<Bin> Bins => Set<Bin>();
        public DbSet<SystemSettings> Settings => Set<SystemSettings>();
        public DbSet<Country> Countries => Set<Country>();
        public Guid? CurrentTenantId => tenantProvider.CurrentTenantId;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemSettings>().Ignore(e => e.DomainEvents);
            modelBuilder.Entity<Country>().Ignore(e => e.DomainEvents);
            modelBuilder.ApplyTenantQueryFilters(this);
        }

        public override int SaveChanges()
        {
            ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
            return base.SaveChanges();
        }
    }

    private readonly string _database = Guid.NewGuid().ToString();

    private TestDbContext Context(Guid? tenantId) => new(
        new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(_database).Options,
        new FixedTenantProvider(tenantId));

    [Fact]
    public void Poco_IsStampedAndFiltered_ThroughItsPrivateSetter()
    {
        using (var a = Context(TenantA))
        {
            a.Bins.Add(new Bin { Code = "A-1" });
            a.SaveChanges();
        }

        using (var a = Context(TenantA))
            a.Bins.Single().TenantId.Should().Be(TenantA);
        using (var b = Context(TenantB))
            b.Bins.Should().BeEmpty();
    }

    [Fact]
    public void PlatformWide_IsVisibleToEveryTenant_ButOnlyPlatformMayChangeIt()
    {
        using (var seed = Context(null))
        {
            seed.Settings.Add(new SystemSettings { Flag = false });
            seed.SaveChanges();
        }

        using (var tenant = Context(TenantA))
        {
            var row = tenant.Settings.Single();
            row.Flag = true;
            var act = () => tenant.SaveChanges();
            act.Should().Throw<TenantIsolationViolationException>();
        }

        using var platform = Context(Platform);
        platform.Settings.Single().Flag = true;
        platform.SaveChanges();
        platform.Settings.Single().TenantId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void PlatformWide_DefaultRow_MayBeCreatedLazilyByAnyTenant()
    {
        using var tenant = Context(TenantA);
        tenant.Settings.Add(new SystemSettings());

        tenant.SaveChanges();

        tenant.Settings.Single().TenantId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void SharedReference_TenantSeesPlatformRowsPlusItsOwn()
    {
        using (var platform = Context(Platform))
        {
            platform.Countries.Add(new Country { Code = "VN" });
            platform.SaveChanges();
        }
        using (var a = Context(TenantA))
        {
            a.Countries.Add(new Country { Code = "A-ONLY" });
            a.SaveChanges();
        }

        using (var a = Context(TenantA))
            a.Countries.Select(c => c.Code).Should().BeEquivalentTo("VN", "A-ONLY");
        using (var b = Context(TenantB))
            b.Countries.Select(c => c.Code).Should().BeEquivalentTo("VN");
    }

    [Fact]
    public void SharedReference_TenantCannotChangeOrDeletePlatformRows()
    {
        using (var platform = Context(Platform))
        {
            platform.Countries.Add(new Country { Code = "VN" });
            platform.SaveChanges();
        }

        using (var a = Context(TenantA))
        {
            a.Countries.Single().Code = "HACKED";
            var update = () => a.SaveChanges();
            update.Should().Throw<TenantIsolationViolationException>();
        }

        using (var a = Context(TenantA))
        {
            a.Countries.Remove(a.Countries.Single());
            var delete = () => a.SaveChanges();
            delete.Should().Throw<TenantIsolationViolationException>();
        }

        using var platformRead = Context(Platform);
        platformRead.Countries.Single().Code.Should().Be("VN");
    }

    [Fact]
    public void SharedReference_TenantCannotCreateRowsInThePlatformTenant()
    {
        using var a = Context(TenantA);
        a.Countries.Add(new Country { Code = "X", TenantId = Platform });

        var act = () => a.SaveChanges();

        act.Should().Throw<TenantIsolationViolationException>();
    }
}
