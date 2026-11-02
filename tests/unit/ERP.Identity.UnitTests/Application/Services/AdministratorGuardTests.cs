using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Localization;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Repositories;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Application.Services;

/// <summary>R91: a shop cannot lock itself out. Runs the rules against the real EF model (InMemory), so the "who is an administrator" query is exercised too.</summary>
public class AdministratorGuardTests
{
    private sealed class World
    {
        public AuthDbContext Db = new(new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Mock<IDomainEventDispatcher>().Object);
        public AuthTenant Shop = AuthTenant.Create("SHOP", "Shop");
        public Permission Manage = Permission.Create(PermissionConstants.Identity_UsersManage, "Manage users");
        public Permission Other = Permission.Create("crm:contacts:view", "View contacts");
        public Role AdminRole = null!;
        public Role OpsRole = null!;
        public Role SalesRole = null!;
        public Mock<ISessionService> Sessions = new();
        public Mock<IRoleRepository> Roles = new();
        public Mock<IPermissionRepository> Permissions = new();
        public Mock<ITokenRevocationQueue> Revocations = new();
        public UserService Users = null!;
        public RoleService RoleService = null!;
        public TenantService TenantService = null!;

        public User Add(string email, params Role[] roles)
        {
            // the tenant and the roles are read from the context: attaching the detached instances again would insert them twice
            var user = User.Create(email, email, "hash", Shop.Id, UserType.Customer);
            user.AssignToTenant(Db.Tenants.Find(Shop.Id)!);
            foreach (var role in roles) user.AssignRole(Db.Roles.Find(role.Id)!, Shop.Id);
            Db.Users.Add(user);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return user;
        }

        public static World Create()
        {
            var w = new World();
            w.AdminRole = Role.Create("ADMIN", "Administrator", w.Shop.Id);
            w.OpsRole = Role.Create("OPS", "Operations", w.Shop.Id);
            w.SalesRole = Role.Create("SALES", "Sales", w.Shop.Id);
            w.Db.Tenants.Add(w.Shop);
            w.Db.Permissions.AddRange(w.Manage, w.Other);
            w.Db.Roles.AddRange(w.AdminRole, w.OpsRole, w.SalesRole);
            w.Db.RolePermissions.AddRange(
                new RolePermission { RoleId = w.AdminRole.Id, PermissionId = w.Manage.Id },
                new RolePermission { RoleId = w.OpsRole.Id, PermissionId = w.Manage.Id },
                new RolePermission { RoleId = w.SalesRole.Id, PermissionId = w.Other.Id });
            w.Db.SaveChanges();
            w.Db.ChangeTracker.Clear();

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(u => u.Users).Returns(new UserRepository(w.Db));
            uow.SetupGet(u => u.Roles).Returns(w.Roles.Object);
            uow.SetupGet(u => u.Permissions).Returns(w.Permissions.Object);
            uow.SetupGet(u => u.Revocations).Returns(w.Revocations.Object);
            var tenants = new Mock<ITenantRepository>();
            uow.SetupGet(u => u.Tenants).Returns(tenants.Object);
            uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(async ct => await w.Db.SaveChangesAsync(ct));
            tenants.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => AuthTenant.Create("OTHER", "Other"));
            w.Roles.Setup(r => r.GetByIdInTenantAsync(It.IsAny<Guid>(), w.Shop.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Guid _, CancellationToken __) => w.Db.Roles.AsNoTracking().FirstOrDefault(r => r.Id == id));
            w.Permissions.Setup(p => p.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Permission> { w.Manage, w.Other });

            w.Users = new UserService(uow.Object, Mock.Of<IPasswordHasher>(), Mock.Of<ISecurityAuditService>(), Mock.Of<IPasswordPolicyService>(),
                Mock.Of<IStringLocalizer<AuthMessages>>(), w.Sessions.Object, new ConfigurationBuilder().Build());
            w.RoleService = new RoleService(uow.Object);
            w.TenantService = new TenantService(uow.Object);
            return w;
        }

        public async Task<User> ReloadAsync(Guid id) => await Db.Users.AsNoTracking().FirstAsync(u => u.Id == id);
    }

    private static async Task Refused(Func<Task> act, string code) =>
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().NotBeNullOrWhiteSpace().And.Subject.Should().NotBeNull(code);

    // --- lock / deactivate

    [Fact]
    public async Task You_cannot_lock_or_deactivate_yourself_and_nothing_is_saved_or_revoked()
    {
        var w = World.Create();
        var me = w.Add("me@shop.test", w.AdminRole);
        w.Add("other@shop.test", w.AdminRole);

        await Refused(() => w.Users.LockAsync(me.Id, w.Shop.Id, me.Id, default), "self");
        await Refused(() => w.Users.DeactivateAsync(me.Id, w.Shop.Id, me.Id, default), "self");

        var reloaded = await w.ReloadAsync(me.Id);
        reloaded.IsLocked.Should().BeFalse();
        reloaded.IsActive.Should().BeTrue();
        w.Sessions.Verify(s => s.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_locked_or_deactivated_by_someone_else()
    {
        var w = World.Create();
        var admin = w.Add("admin@shop.test", w.AdminRole);
        var clerk = w.Add("clerk@shop.test", w.SalesRole);

        await Refused(() => w.Users.LockAsync(admin.Id, w.Shop.Id, clerk.Id, default), "last");
        await Refused(() => w.Users.DeactivateAsync(admin.Id, w.Shop.Id, clerk.Id, default), "last");

        (await w.ReloadAsync(admin.Id)).IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task An_administrator_can_be_locked_while_another_active_unlocked_one_remains()
    {
        var w = World.Create();
        var first = w.Add("a1@shop.test", w.AdminRole);
        var second = w.Add("a2@shop.test", w.OpsRole); // admin through a different role

        await w.Users.LockAsync(first.Id, w.Shop.Id, second.Id, default);

        (await w.ReloadAsync(first.Id)).IsLocked.Should().BeTrue();
        w.Sessions.Verify(s => s.RevokeAllForUserAsync(first.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_second_administrator_who_is_locked_or_inactive_does_not_count()
    {
        var w = World.Create();
        var first = w.Add("a1@shop.test", w.AdminRole);
        var locked = w.Add("a2@shop.test", w.AdminRole);
        var gone = w.Add("a3@shop.test", w.AdminRole);
        var lockedEntity = await w.Db.Users.FirstAsync(u => u.Id == locked.Id); lockedEntity.Lock();
        var goneEntity = await w.Db.Users.FirstAsync(u => u.Id == gone.Id); goneEntity.Deactivate();
        await w.Db.SaveChangesAsync(); w.Db.ChangeTracker.Clear();
        var clerk = w.Add("clerk@shop.test", w.SalesRole);

        await Refused(() => w.Users.LockAsync(first.Id, w.Shop.Id, clerk.Id, default), "last");
    }

    [Fact]
    public async Task A_member_who_is_not_an_administrator_can_be_locked_and_deactivated_and_activated_again()
    {
        var w = World.Create();
        w.Add("admin@shop.test", w.AdminRole);
        var clerk = w.Add("clerk@shop.test", w.SalesRole);
        var admin = await w.Db.Users.AsNoTracking().FirstAsync(u => u.Email == "admin@shop.test");

        await w.Users.LockAsync(clerk.Id, w.Shop.Id, admin.Id, default);
        await w.Users.DeactivateAsync(clerk.Id, w.Shop.Id, admin.Id, default);
        var afterDeactivate = await w.ReloadAsync(clerk.Id);
        afterDeactivate.IsLocked.Should().BeTrue();
        afterDeactivate.IsActive.Should().BeFalse();

        await w.Users.ActivateAsync(clerk.Id, w.Shop.Id, default);
        (await w.ReloadAsync(clerk.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Activate_only_reaches_members_of_the_callers_tenant()
    {
        var w = World.Create();
        var clerk = w.Add("clerk@shop.test", w.SalesRole);

        var act = () => w.Users.ActivateAsync(clerk.Id, Guid.NewGuid(), default);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    // --- roles of a user

    [Fact]
    public async Task Taking_the_only_admin_role_from_the_last_administrator_is_refused_but_allowed_with_a_second_administrator_or_another_admin_role()
    {
        var w = World.Create();
        var admin = w.Add("admin@shop.test", w.AdminRole);

        await Refused(() => w.Users.RemoveRoleAsync(admin.Id, w.AdminRole.Id, w.Shop.Id, default), "last");

        w.Add("second@shop.test", w.OpsRole);
        await w.Users.RemoveRoleAsync(admin.Id, w.AdminRole.Id, w.Shop.Id, default);
        (await w.Db.UserRoles.CountAsync(ur => ur.UserId == admin.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Removing_one_of_two_admin_roles_of_the_last_administrator_is_fine()
    {
        var w = World.Create();
        var admin = w.Add("admin@shop.test", w.AdminRole, w.OpsRole);

        await w.Users.RemoveRoleAsync(admin.Id, w.AdminRole.Id, w.Shop.Id, default);

        (await w.Db.UserRoles.CountAsync(ur => ur.UserId == admin.Id && ur.RoleId == w.OpsRole.Id)).Should().Be(1);
    }

    // --- roles

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("STAFF")]
    [InlineData("USER")]
    public async Task The_default_roles_cannot_be_deleted(string code)
    {
        var w = World.Create();
        var role = Role.Create(code, code, w.Shop.Id);
        w.Db.Roles.Add(role); await w.Db.SaveChangesAsync(); w.Db.ChangeTracker.Clear();

        await Refused(() => w.RoleService.DeleteAsync(role.Id, w.Shop.Id, default), "default");

        w.Roles.Verify(r => r.DeleteAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_custom_role_without_user_management_can_be_deleted()
    {
        var w = World.Create();
        w.Add("admin@shop.test", w.AdminRole);

        await w.RoleService.DeleteAsync(w.SalesRole.Id, w.Shop.Id, default);

        w.Roles.Verify(r => r.DeleteAsync(It.Is<Role>(x => x.Id == w.SalesRole.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_role_that_makes_the_only_administrators_cannot_be_deleted_or_stripped_of_user_management()
    {
        var w = World.Create();
        w.Add("admin@shop.test", w.AdminRole);

        await Refused(() => w.RoleService.DeleteAsync(w.AdminRole.Id, w.Shop.Id, default), "last");
        await Refused(() => w.RoleService.RemovePermissionAsync(w.AdminRole.Id, w.Manage.Id, w.Shop.Id, default), "last");

        w.Permissions.Verify(p => p.RemoveFromRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_admin_role_can_go_when_another_role_keeps_an_administrator()
    {
        var w = World.Create();
        w.Add("admin@shop.test", w.AdminRole);
        w.Add("ops@shop.test", w.OpsRole);

        await w.RoleService.DeleteAsync(w.AdminRole.Id, w.Shop.Id, default);

        w.Roles.Verify(r => r.DeleteAsync(It.Is<Role>(x => x.Id == w.AdminRole.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Removing_a_permission_that_is_not_user_management_is_never_blocked()
    {
        var w = World.Create();
        w.Add("admin@shop.test", w.AdminRole);

        await w.RoleService.RemovePermissionAsync(w.SalesRole.Id, w.Other.Id, w.Shop.Id, default);

        w.Permissions.Verify(p => p.RemoveFromRoleAsync(w.SalesRole.Id, w.Other.Id, w.Shop.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- tenant

    [Fact]
    public async Task The_platform_tenant_cannot_be_deactivated_but_another_tenant_can()
    {
        var w = World.Create();

        await Refused(() => w.TenantService.DeactivateAsync(PlatformTenant.Id, default), "platform");
        await w.TenantService.DeactivateAsync(Guid.NewGuid(), default);
    }
}
