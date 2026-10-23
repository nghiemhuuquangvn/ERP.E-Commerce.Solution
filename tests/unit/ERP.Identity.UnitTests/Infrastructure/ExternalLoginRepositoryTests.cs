using ERP.Identity.Application.Interfaces;
using ERP.Identity.Domain.Constants;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Infrastructure;

/// <summary>Runs the external-login queries against the real EF model (InMemory provider).</summary>
public class ExternalLoginRepositoryTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static AuthDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Mock<IDomainEventDispatcher>().Object);

    private static async Task<(User User, ExternalLogin Link)> SeedLinkedUserAsync(AuthDbContext db, Guid tenant)
    {
        var user = User.Create($"{Guid.NewGuid():N}@acme.test", "u", "hash", tenant, UserType.Customer);
        var link = ExternalLogin.Create(user.Id, tenant, "Google", "g-1", user.Email);
        db.Users.Add(user);
        db.ExternalLogins.Add(link);
        await db.SaveChangesAsync();
        return (user, link);
    }

    [Fact]
    public async Task GetByExternalProviderAsync_ReturnsNull_WhenTheExternalAccountIsNew()
    {
        await using var db = NewContext();

        var user = await new UserRepository(db).GetByExternalProviderAsync("Google", "never-seen", TenantA, CancellationToken.None);

        user.Should().BeNull("the caller provisions or links on null; it used to throw 'User not found' so nobody could ever sign up");
    }

    [Fact]
    public async Task GetByExternalProviderAsync_FindsTheUser_OnlyInTheTenantOfTheLink_AndOnlyWhileActive()
    {
        await using var db = NewContext();
        var (seeded, link) = await SeedLinkedUserAsync(db, TenantA);
        var repository = new UserRepository(db);

        (await repository.GetByExternalProviderAsync("Google", "g-1", TenantA, CancellationToken.None))!.Id.Should().Be(seeded.Id);
        (await repository.GetByExternalProviderAsync("Google", "g-1", TenantB, CancellationToken.None)).Should().BeNull();

        link.Disable();
        await db.SaveChangesAsync();
        (await repository.GetByExternalProviderAsync("Google", "g-1", TenantA, CancellationToken.None)).Should().BeNull("an unlinked account no longer signs in");
    }

    [Fact]
    public async Task ExternalLoginRepository_Add_DoesNotSaveByItself_TheUnitOfWorkCommits()
    {
        await using var db = NewContext();
        var repository = new ExternalLoginRepository(db);
        var link = ExternalLogin.Create(Guid.NewGuid(), TenantA, "Google", "g-2", "a@b.test");

        await repository.AddAsync(link, CancellationToken.None);

        db.ChangeTracker.Entries<ExternalLogin>().Should().ContainSingle(e => e.State == EntityState.Added);
        (await db.ExternalLogins.AsNoTracking().CountAsync()).Should().Be(0);
        await db.SaveChangesAsync();
        (await db.ExternalLogins.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ExternalLoginRepository_GetAll_ByIdAndUnlink_Work()
    {
        await using var db = NewContext();
        var (_, link) = await SeedLinkedUserAsync(db, TenantA);
        await SeedLinkedUserAsync(db, TenantB);
        var repository = new ExternalLoginRepository(db);

        (await repository.GetByIdAsync(link.Id, CancellationToken.None)).Provider.Should().Be("Google");
        var inTenantA = await repository.GetAllAsync(l => l.TenantId == TenantA, ct: CancellationToken.None);
        inTenantA.Should().ContainSingle();

        await repository.DeleteAsync(link, CancellationToken.None);
        await db.SaveChangesAsync();
        (await db.ExternalLogins.AsNoTracking().SingleAsync(l => l.Id == link.Id)).IsActive.Should().BeFalse("unlinking keeps the row");

        var missing = () => repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);
        await missing.Should().ThrowAsync<KeyNotFoundException>();
    }
}
