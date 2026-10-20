using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ERP.Identity.Api.Authorization;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Security;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.Identity.UnitTests.Security;

/// <summary>
/// Tenant isolation of users/roles/permissions (2026-10-20 review): platform vs tenant
/// permission scope, anti-escalation on every grant path, the cross-tenant access policy,
/// and tenant-from-host resolution.
/// </summary>
public class AuthorizationScopeTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid Platform = PlatformTenant.Id;

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IPermissionRepository> _permissions = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IUserRepository> _users = new();

    public AuthorizationScopeTests()
    {
        _uow.Setup(u => u.Permissions).Returns(_permissions.Object);
        _uow.Setup(u => u.Roles).Returns(_roles.Object);
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _roles.Setup(r => r.GetRolesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        _permissions.Setup(p => p.GetPermissionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        _permissions.Setup(p => p.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Permission(PermissionConstants.Identity_UsersView),
                Permission(PermissionConstants.Identity_RolesManage),
                Permission(PermissionConstants.Settings_SystemManage),
                Permission(PermissionConstants.Identity_TenantsManage),
            });
    }

    private static Permission Permission(string code) => new() { Id = Guid.NewGuid(), PermissionCode = code, Module = "Identity" };

    private void UserHas(Guid userId, Guid tenantId, params string[] codes) =>
        _permissions.Setup(p => p.GetPermissionAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(codes.ToList());

    // ---- Token contents ----

    [Fact]
    public async Task EffectivePermissions_TenantToken_NeverContainsPlatformPermissions_EvenIfDbHasThem()
    {
        var user = Guid.NewGuid();
        UserHas(user, TenantA, PermissionConstants.Identity_UsersView, PermissionConstants.Settings_SystemManage, PermissionConstants.Identity_TenantsView);

        var result = await EffectivePermissions.ResolveAsync(_uow.Object, user, TenantA, new[] { "ADMIN" }, CancellationToken.None);

        result.Should().BeEquivalentTo(PermissionConstants.Identity_UsersView);
    }

    [Fact]
    public async Task EffectivePermissions_SuperAdminInPlatformTenant_GetsWholeCatalog()
    {
        var result = await EffectivePermissions.ResolveAsync(_uow.Object, Guid.NewGuid(), Platform, new[] { PlatformRoles.SuperAdmin }, CancellationToken.None);

        result.Should().Contain(PermissionConstants.Settings_SystemManage).And.Contain(PermissionConstants.Identity_TenantsManage);
    }

    [Fact]
    public async Task EffectivePermissions_RoleNamedSuperAdminInOrdinaryTenant_GetsNothingExtra()
    {
        var user = Guid.NewGuid();
        UserHas(user, TenantA, PermissionConstants.Identity_UsersView);

        var result = await EffectivePermissions.ResolveAsync(_uow.Object, user, TenantA, new[] { "SuperAdmin" }, CancellationToken.None);

        result.Should().BeEquivalentTo(PermissionConstants.Identity_UsersView);
    }

    // ---- Role grants ----

    private RoleService RoleServiceWithRole(Guid roleId, Guid tenantId)
    {
        _roles.Setup(r => r.GetByIdInTenantAsync(roleId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Role { Id = roleId, RoleCode = "ADMIN", TenantId = tenantId });
        return new RoleService(_uow.Object);
    }

    [Fact]
    public async Task AssignPermission_PlatformPermissionInOrdinaryTenant_IsRejected_EvenIfCallerHoldsIt()
    {
        var roleId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        UserHas(caller, TenantA, PermissionConstants.Settings_SystemManage);

        var act = () => RoleServiceWithRole(roleId, TenantA)
            .AssignPermissionAsync(roleId, PermissionConstants.Settings_SystemManage, TenantA, caller, "admin", CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _permissions.Verify(p => p.AssignToRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignPermission_PermissionTheCallerDoesNotHold_IsRejected()
    {
        var roleId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        UserHas(caller, TenantA, PermissionConstants.Identity_RolesManage);

        var act = () => RoleServiceWithRole(roleId, TenantA)
            .AssignPermissionAsync(roleId, PermissionConstants.Identity_UsersView, TenantA, caller, "admin", CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task AssignPermission_PlatformPermissionInPlatformTenantBySuperAdmin_IsAllowed()
    {
        var roleId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        _roles.Setup(r => r.GetRolesAsync(caller, Platform, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { PlatformRoles.SuperAdmin });
        _users.Setup(u => u.GetUserIdsByRoleAsync(roleId, Platform, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Guid>());

        await RoleServiceWithRole(roleId, Platform)
            .AssignPermissionAsync(roleId, PermissionConstants.Settings_SystemManage, Platform, caller, "root", CancellationToken.None);

        _permissions.Verify(p => p.AssignToRoleAsync(roleId, It.IsAny<Guid>(), Platform, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("superadmin")]
    [InlineData("SUPER_ADMIN")]
    [InlineData("Super-Admin")]
    [InlineData("SYSTEM")]
    public async Task CreateRole_ReservedCode_IsRejected(string code)
    {
        var act = () => new RoleService(_uow.Object).CreateAsync(TenantA, new CreateRoleRequest(code, "x"), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _roles.Verify(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- API keys ----

    private ApiKeyService ApiKeyService()
    {
        _uow.Setup(u => u.ApiKeys).Returns(new Mock<IApiKeyRepository>().Object);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("hash");
        return new ApiKeyService(_uow.Object, hasher.Object);
    }

    [Theory]
    [InlineData(PermissionConstants.Settings_SystemManage)] // platform scope
    [InlineData(PermissionConstants.Identity_RolesManage)]  // not held by the creator
    [InlineData("made:up:permission")]                      // unknown
    public async Task CreateApiKey_UngrantableScope_IsRejected(string scope)
    {
        var creator = Guid.NewGuid();
        UserHas(creator, TenantA, PermissionConstants.Identity_UsersView, PermissionConstants.Settings_SystemManage);

        var act = () => ApiKeyService().CreateAsync(TenantA, "key", scope, null, creator.ToString(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task CreateApiKey_ScopeHeldByCreator_IsCreated()
    {
        var creator = Guid.NewGuid();
        UserHas(creator, TenantA, PermissionConstants.Identity_UsersView);

        var result = await ApiKeyService().CreateAsync(TenantA, "key", $" {PermissionConstants.Identity_UsersView} ", null, creator.ToString(), CancellationToken.None);

        result.Key.Scopes.Should().Be(PermissionConstants.Identity_UsersView);
    }

    // ---- Cross-tenant policy ----

    private TenantAccessPolicy Policy(Guid callerUser, Guid callerTenant)
    {
        var userContext = new Mock<IUserContextService>();
        userContext.Setup(u => u.GetCurrentUser()).Returns(new UserContext
        {
            UserId = callerUser.ToString(),
            TenantId = callerTenant.ToString(),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        return new TenantAccessPolicy(userContext.Object, _uow.Object);
    }

    [Fact]
    public async Task Policy_OwnTenant_IsAllowed()
    {
        (await Policy(Guid.NewGuid(), TenantA).CanAccessTenantAsync(TenantA, PermissionConstants.Settings_SystemManage, CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Policy_OtherTenantFromOrdinaryTenant_IsDenied_EvenWithPermissionInDb()
    {
        var caller = Guid.NewGuid();
        UserHas(caller, TenantA, PermissionConstants.Settings_SystemManage, PermissionConstants.Identity_TenantsView);

        (await Policy(caller, TenantA).CanAccessTenantAsync(TenantB, PermissionConstants.Settings_SystemManage, CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Policy_OtherTenantFromPlatformTenant_RequiresThePermission()
    {
        var withPermission = Guid.NewGuid();
        var withoutPermission = Guid.NewGuid();
        UserHas(withPermission, Platform, PermissionConstants.Settings_SystemManage);
        UserHas(withoutPermission, Platform, PermissionConstants.Identity_UsersView);

        (await Policy(withPermission, Platform).CanAccessTenantAsync(TenantB, PermissionConstants.Settings_SystemManage, CancellationToken.None))
            .Should().BeTrue();
        (await Policy(withoutPermission, Platform).CanAccessTenantAsync(TenantB, PermissionConstants.Settings_SystemManage, CancellationToken.None))
            .Should().BeFalse();
    }

    // ---- [TenantRouteAccess] ----

    private static ActionExecutingContext ActionContext(Guid? routeTenant, bool policyAllows, out Mock<ITenantAccessPolicy> policy)
    {
        policy = new Mock<ITenantAccessPolicy>();
        policy.Setup(p => p.CanAccessTenantAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(policyAllows);
        var services = new ServiceCollection().AddSingleton(policy.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var routeData = new RouteData();
        if (routeTenant is not null)
            routeData.Values["tenantId"] = routeTenant.Value.ToString();
        var actionContext = new ActionContext(http, routeData, new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: new object());
    }

    [Fact]
    public async Task TenantRouteAccess_DeniedTenant_ShortCircuitsWithForbid()
    {
        var context = ActionContext(TenantB, policyAllows: false, out _);
        var nextCalled = false;

        await new TenantRouteAccessAttribute(PermissionConstants.Settings_SystemManage)
            .OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        context.Result.Should().BeOfType<ForbidResult>();
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task TenantRouteAccess_NoTenantInRequest_DoesNotConsultPolicy()
    {
        var context = ActionContext(null, policyAllows: false, out var policy);
        var nextCalled = false;

        await new TenantRouteAccessAttribute(PermissionConstants.Settings_SystemManage)
            .OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        nextCalled.Should().BeTrue();
        policy.Verify(p => p.CanAccessTenantAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- [AuthorizePermission]: no role-name bypass ----

    [Fact]
    public async Task AuthorizePermission_RoleNamedSuperAdminWithoutPermission_IsForbidden()
    {
        var userContext = new Mock<IUserContextService>();
        userContext.Setup(u => u.GetCurrentUser()).Returns(new UserContext
        {
            UserId = Guid.NewGuid().ToString(),
            TenantId = TenantA.ToString(),
            Roles = new[] { "SuperAdmin" },
            Permissions = Array.Empty<string>(),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(userContext.Object).BuildServiceProvider()
        };
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>());

        await new AuthorizePermissionAttribute(PermissionConstants.Settings_SystemManage).OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ---- Tenant from host ----

    [Theory]
    [InlineData("Shop.Acme.COM", "shop.acme.com")]
    [InlineData("shop.acme.com:443", "shop.acme.com")]
    [InlineData("shop.acme.com.", "shop.acme.com")]
    public void TenantDomain_NormalizesHost(string input, string expected) =>
        TenantDomain.NormalizeHost(input).Should().Be(expected);

    [Theory]
    [InlineData("localhost")]
    [InlineData("10.0.0.1")]
    [InlineData("bad host.com")]
    public void TenantDomain_RejectsNonDnsHosts(string host) =>
        FluentActions.Invoking(() => TenantDomain.Create(TenantA, host, false)).Should().Throw<ArgumentException>();

    private static (AuthDbContext Db, AuthTenant Acme, AuthTenant Inactive) SeedTenants()
    {
        var db = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Mock<IDomainEventDispatcher>().Object);

        var system = AuthTenant.Create(PlatformTenant.Code, "System");
        system.Id = PlatformTenant.Id;
        var acme = AuthTenant.Create("ACME", "Acme");
        var inactive = AuthTenant.Create("GONE", "Gone");
        inactive.Deactivate();
        db.Tenants.AddRange(system, acme, inactive);
        db.Set<TenantDomain>().Add(TenantDomain.Create(acme.Id, "shop.acme.com", true));
        db.SaveChanges();
        return (db, acme, inactive);
    }

    private static TenantDomainService Resolver(AuthDbContext db, string? fallback = null) => new(db,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tenancy:BaseDomain"] = "erp.com",
            ["Tenancy:StorefrontBaseDomain"] = "shop.erp.com",
            ["Tenancy:PlatformHost"] = "admin.erp.com",
            ["Tenancy:FallbackTenantCode"] = fallback,
        }).Build());

    [Fact]
    public async Task ResolveHost_MapsPlatformCustomAndSubdomainHosts()
    {
        var (db, acme, _) = SeedTenants();
        var resolver = Resolver(db);

        (await resolver.ResolveAsync("admin.erp.com", default))!.IsPlatform.Should().BeTrue();
        (await resolver.ResolveAsync("SHOP.acme.com:443", default))!.TenantId.Should().Be(acme.Id);
        (await resolver.ResolveAsync("acme.erp.com", default))!.TenantId.Should().Be(acme.Id);
        (await resolver.ResolveAsync("acme.shop.erp.com", default))!.TenantId.Should().Be(acme.Id);
    }

    [Theory]
    [InlineData("evil.com")]           // unknown host
    [InlineData("x.acme.erp.com")]     // nested subdomain
    [InlineData("gone.erp.com")]       // inactive tenant
    [InlineData("erp.com")]            // bare base domain
    [InlineData("shop.erp.com")]       // bare storefront base domain (no tenant label)
    public async Task ResolveHost_UnknownOrInactive_ResolvesNothing(string host)
    {
        var (db, _, _) = SeedTenants();

        (await Resolver(db).ResolveAsync(host, default)).Should().BeNull();
    }

    [Fact]
    public async Task ResolveHost_FallbackOnlyWhenConfigured()
    {
        var (db, acme, _) = SeedTenants();

        (await Resolver(db, fallback: "ACME").ResolveAsync("localhost", default))!.TenantId.Should().Be(acme.Id);
    }
}
