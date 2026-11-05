using ERP.Identity.Application.Interfaces;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Infrastructure;

/// <summary>
/// "Who holds permission X in tenant T" (NotificationService asks it to find the approvers). It mirrors what a token carries: an active role of the
/// tenant that holds the permission, platform permissions only in the platform tenant, the platform super-admin role holding the whole catalog there.
/// </summary>
public class UsersWithPermissionTests
{
    private const string Approve = "purchase_order:approve";

    private sealed class World
    {
        public AuthDbContext Db = new(new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Mock<IDomainEventDispatcher>().Object);
        public AuthTenant Shop = AuthTenant.Create("SHOP", "Shop");
        public AuthTenant Other = AuthTenant.Create("OTHER", "Other");
        public Permission Approving = Permission.Create(Approve, "Approve purchase orders");
        public Permission Viewing = Permission.Create("crm:contacts:view", "View contacts");
        public Role Approvers = null!;
        public Role Viewers = null!;
        public PermissionRepository Repo => new(Db);

        public World()
        {
            Approvers = Role.Create("APPROVERS", "Approvers", Shop.Id);
            Viewers = Role.Create("VIEWERS", "Viewers", Shop.Id);
            Db.Tenants.AddRange(Shop, Other);
            Db.Permissions.AddRange(Approving, Viewing);
            Db.Roles.AddRange(Approvers, Viewers);
            Db.RolePermissions.AddRange(
                new RolePermission { RoleId = Approvers.Id, PermissionId = Approving.Id },
                new RolePermission { RoleId = Viewers.Id, PermissionId = Viewing.Id });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        public User Add(string email, AuthTenant tenant, params Role[] roles)
        {
            var user = User.Create(email, email, "hash", tenant.Id, UserType.Customer);
            user.AssignToTenant(Db.Tenants.Find(tenant.Id)!);
            foreach (var role in roles) user.AssignRole(Db.Roles.Find(role.Id)!, tenant.Id);
            Db.Users.Add(user);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return user;
        }
    }

    [Fact]
    public async Task Holders_of_a_role_that_has_the_permission_are_listed_and_others_are_not()
    {
        var w = new World();
        var ann = w.Add("ann@x.io", w.Shop, w.Approvers);
        var binh = w.Add("binh@x.io", w.Shop, w.Viewers);
        w.Add("chi@x.io", w.Shop);

        var ids = await w.Repo.GetUserIdsWithPermissionAsync(Approve, w.Shop.Id, default);

        ids.Should().Equal(ann.Id);
        ids.Should().NotContain(binh.Id);
    }

    [Fact]
    public async Task A_user_with_two_roles_that_both_grant_it_is_listed_once()
    {
        var w = new World();
        var second = Role.Create("APPROVERS-2", "Approvers 2", w.Shop.Id);
        w.Db.Roles.Add(second);
        w.Db.RolePermissions.Add(new RolePermission { RoleId = second.Id, PermissionId = w.Approving.Id });
        w.Db.SaveChanges();
        w.Db.ChangeTracker.Clear();
        var ann = w.Add("ann@x.io", w.Shop, w.Approvers, second);

        var ids = await w.Repo.GetUserIdsWithPermissionAsync(Approve, w.Shop.Id, default);

        ids.Should().Equal(ann.Id);
    }

    [Fact]
    public async Task A_deactivated_role_grants_nothing()
    {
        var w = new World();
        w.Add("ann@x.io", w.Shop, w.Approvers);
        var role = await w.Db.Roles.FirstAsync(r => r.Id == w.Approvers.Id);
        typeof(Role).GetProperty(nameof(Role.IsActive))!.SetValue(role, false);
        await w.Db.SaveChangesAsync();

        (await w.Repo.GetUserIdsWithPermissionAsync(Approve, w.Shop.Id, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_deactivated_user_is_not_listed()
    {
        var w = new World();
        var ann = w.Add("ann@x.io", w.Shop, w.Approvers);
        var stored = await w.Db.Users.FirstAsync(u => u.Id == ann.Id);
        stored.Deactivate();
        await w.Db.SaveChangesAsync();

        (await w.Repo.GetUserIdsWithPermissionAsync(Approve, w.Shop.Id, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Another_tenants_holders_and_roles_do_not_count()
    {
        var w = new World();
        var foreignRole = Role.Create("APPROVERS", "Approvers", w.Other.Id);
        w.Db.Roles.Add(foreignRole);
        w.Db.RolePermissions.Add(new RolePermission { RoleId = foreignRole.Id, PermissionId = w.Approving.Id });
        w.Db.SaveChanges();
        w.Db.ChangeTracker.Clear();
        var theirs = w.Add("theirs@x.io", w.Other, foreignRole);
        var ours = w.Add("ours@x.io", w.Shop, w.Approvers);

        (await w.Repo.GetUserIdsWithPermissionAsync(Approve, w.Shop.Id, default)).Should().Equal(ours.Id);
        (await w.Repo.GetUserIdsWithPermissionAsync(Approve, w.Other.Id, default)).Should().Equal(theirs.Id);
    }

    [Fact]
    public async Task A_role_row_of_another_tenant_linked_to_this_tenant_by_bad_data_grants_nothing()
    {
        var w = new World();
        var foreignRole = Role.Create("EVIL", "Evil", w.Other.Id);
        w.Db.Roles.Add(foreignRole);
        w.Db.RolePermissions.Add(new RolePermission { RoleId = foreignRole.Id, PermissionId = w.Approving.Id });
        w.Db.SaveChanges();
        w.Db.ChangeTracker.Clear();
        var user = w.Add("ann@x.io", w.Shop);
        w.Db.UserRoles.Add((UserRole)Activator.CreateInstance(typeof(UserRole), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object[] { user.Id, foreignRole.Id, w.Shop.Id }, null)!);
        w.Db.SaveChanges();

        (await w.Repo.GetUserIdsWithPermissionAsync(Approve, w.Shop.Id, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_platform_permission_is_never_held_outside_the_platform_tenant()
    {
        var w = new World();
        var platformCode = PermissionConstants.Identity_TenantsManage;
        var perm = Permission.Create(platformCode, "Manage tenants");
        w.Db.Permissions.Add(perm);
        w.Db.RolePermissions.Add(new RolePermission { RoleId = w.Approvers.Id, PermissionId = perm.Id });
        w.Db.SaveChanges();
        w.Db.ChangeTracker.Clear();
        w.Add("ann@x.io", w.Shop, w.Approvers);

        (await w.Repo.GetUserIdsWithPermissionAsync(platformCode, w.Shop.Id, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task In_the_platform_tenant_the_super_admin_role_holds_every_permission()
    {
        var w = new World();
        var platform = AuthTenant.Create("SYSTEM", "Platform");
        typeof(AuthTenant).GetProperty(nameof(AuthTenant.Id))!.SetValue(platform, PlatformTenant.Id);
        var superAdmin = Role.Create(PlatformRoles.SuperAdmin, "Super administrator", PlatformTenant.Id);
        w.Db.Tenants.Add(platform);
        w.Db.Roles.Add(superAdmin);
        w.Db.SaveChanges();
        w.Db.ChangeTracker.Clear();
        var root = w.Add("root@x.io", platform, superAdmin);
        w.Add("viewer@x.io", platform);

        (await w.Repo.GetUserIdsWithPermissionAsync(Approve, PlatformTenant.Id, default)).Should().Equal(root.Id);
    }

    [Fact]
    public async Task The_super_admin_role_of_an_ordinary_tenant_has_no_special_power()
    {
        var w = new World();
        var fake = Role.Create(PlatformRoles.SuperAdmin, "Looks like a super admin", w.Shop.Id);
        w.Db.Roles.Add(fake);
        w.Db.SaveChanges();
        w.Db.ChangeTracker.Clear();
        w.Add("ann@x.io", w.Shop, fake);

        (await w.Repo.GetUserIdsWithPermissionAsync(Approve, w.Shop.Id, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_permission_lists_nobody()
    {
        var w = new World();
        w.Add("ann@x.io", w.Shop, w.Approvers);

        (await w.Repo.GetUserIdsWithPermissionAsync("nope:nothing", w.Shop.Id, default)).Should().BeEmpty();
    }
}
