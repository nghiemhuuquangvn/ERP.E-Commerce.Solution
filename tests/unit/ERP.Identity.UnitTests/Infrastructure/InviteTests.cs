using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Localization;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Repositories;
using ERP.Identity.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Infrastructure;

/// <summary>R89: inviting an existing account into a second tenant answered HTTP 500 (the new membership was saved as an UPDATE of a row that does not exist).</summary>
public class InviteTests
{
    private static AuthDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Mock<IDomainEventDispatcher>().Object);

    private sealed class Fixture
    {
        public AuthDbContext Db = NewContext();
        public AuthTenant Home = AuthTenant.Create("HOME", "Home shop");
        public AuthTenant Shop = AuthTenant.Create("SHOP", "Second shop");
        public User Invitee = null!;
        public Role Staff = null!;
        public UserService Service = null!;

        public static async Task<Fixture> CreateAsync(string email = "ada@example.test")
        {
            var f = new Fixture();
            f.Db.Tenants.AddRange(f.Home, f.Shop);
            f.Invitee = User.Create(email, "ada", "hash", f.Home.Id, UserType.Customer);
            f.Invitee.AssignToTenant(f.Home);
            f.Staff = Role.Create("STAFF", "Staff", f.Shop.Id);
            f.Db.Users.Add(f.Invitee);
            f.Db.Roles.Add(f.Staff);
            await f.Db.SaveChangesAsync();
            f.Db.ChangeTracker.Clear();

            var uow = new Mock<IUnitOfWork>();
            var users = new UserRepository(f.Db);
            var tenants = new Mock<ITenantRepository>();
            tenants.Setup(t => t.GetByIdAsync(f.Shop.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => f.Db.Tenants.Find(f.Shop.Id));
            var roles = new Mock<IRoleRepository>();
            roles.Setup(r => r.GetByCodeAsync("STAFF", f.Shop.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => f.Db.Roles.Find(f.Staff.Id));
            uow.SetupGet(u => u.Users).Returns(users);
            uow.SetupGet(u => u.Tenants).Returns(tenants.Object);
            uow.SetupGet(u => u.Roles).Returns(roles.Object);
            uow.SetupGet(u => u.Revocations).Returns(new Mock<ITokenRevocationQueue>().Object);
            uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(async ct => await f.Db.SaveChangesAsync(ct));

            f.Service = new UserService(uow.Object, Mock.Of<IPasswordHasher>(), Mock.Of<ISecurityAuditService>(), Mock.Of<IPasswordPolicyService>(),
                Mock.Of<IStringLocalizer<AuthMessages>>(), Mock.Of<ISessionService>(), new ConfigurationBuilder().Build());
            return f;
        }
    }

    [Fact]
    public async Task An_existing_account_is_invited_into_a_second_tenant_with_a_role_and_can_be_found_there()
    {
        var f = await Fixture.CreateAsync();

        var result = await f.Service.InviteToTenantAsync(f.Shop.Id, new InviteUserToTenantRequest("ada@example.test", "STAFF"), CancellationToken.None);

        result.AlreadyMember.Should().BeFalse();
        f.Db.ChangeTracker.Clear();
        (await f.Db.Set<AuthUserTenant>().CountAsync(ut => ut.UserId == f.Invitee.Id && ut.TenantId == f.Shop.Id)).Should().Be(1);
        (await f.Db.UserRoles.CountAsync(ur => ur.UserId == f.Invitee.Id && ur.TenantId == f.Shop.Id && ur.RoleId == f.Staff.Id)).Should().Be(1);
        (await new UserRepository(f.Db).GetByIdInTenantAsync(f.Invitee.Id, f.Shop.Id, CancellationToken.None)).Should().NotBeNull();
        (await f.Db.Set<AuthUserTenant>().CountAsync(ut => ut.UserId == f.Invitee.Id && ut.TenantId == f.Home.Id)).Should().Be(1, "the home membership is untouched");
    }

    [Fact]
    public async Task Inviting_without_a_role_adds_only_the_membership()
    {
        var f = await Fixture.CreateAsync();

        await f.Service.InviteToTenantAsync(f.Shop.Id, new InviteUserToTenantRequest("ada@example.test", null), CancellationToken.None);

        f.Db.ChangeTracker.Clear();
        (await f.Db.Set<AuthUserTenant>().CountAsync(ut => ut.UserId == f.Invitee.Id && ut.TenantId == f.Shop.Id)).Should().Be(1);
        (await f.Db.UserRoles.CountAsync(ur => ur.UserId == f.Invitee.Id && ur.TenantId == f.Shop.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_second_invite_is_answered_AlreadyMember_and_changes_nothing()
    {
        var f = await Fixture.CreateAsync();
        await f.Service.InviteToTenantAsync(f.Shop.Id, new InviteUserToTenantRequest("ada@example.test", "STAFF"), CancellationToken.None);
        f.Db.ChangeTracker.Clear();

        var again = await f.Service.InviteToTenantAsync(f.Shop.Id, new InviteUserToTenantRequest("ada@example.test", "STAFF"), CancellationToken.None);

        again.AlreadyMember.Should().BeTrue();
        (await f.Db.Set<AuthUserTenant>().CountAsync(ut => ut.UserId == f.Invitee.Id && ut.TenantId == f.Shop.Id)).Should().Be(1);
    }

    [Fact]
    public async Task An_unknown_role_is_refused_and_no_membership_is_left_behind()
    {
        var f = await Fixture.CreateAsync();

        var act = () => f.Service.InviteToTenantAsync(f.Shop.Id, new InviteUserToTenantRequest("ada@example.test", "NOPE"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        f.Db.ChangeTracker.Clear();
        (await f.Db.Set<AuthUserTenant>().CountAsync(ut => ut.UserId == f.Invitee.Id && ut.TenantId == f.Shop.Id)).Should().Be(0);
    }
}
