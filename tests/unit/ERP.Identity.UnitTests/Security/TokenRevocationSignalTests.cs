using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Localization;
using ERP.Identity.Infrastructure.Security;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Authorization.TokenRevocation;
using FluentAssertions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Security;

/// <summary>
/// Every security-relevant change must reach the shared revocation store (phase 3,
/// decision 2026-10-20) so the other services stop accepting the affected tokens.
/// </summary>
public class TokenRevocationSignalTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Mock<IUnitOfWork> _uow = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<ITokenRevocationQueue> _revocations = new();
    private readonly Mock<ISessionService> _sessions = new();

    public TokenRevocationSignalTests()
    {
        _uow.Setup(u => u.Revocations).Returns(_revocations.Object);
    }

    private UserService UserService() => new(
        _uow.Object, new Mock<IPasswordHasher>().Object, new Mock<ISecurityAuditService>().Object,
        new Mock<IPasswordPolicyService>().Object, new Mock<IStringLocalizer<AuthMessages>>().Object, _sessions.Object);

    private User NewUser() => User.Create("u@example.test", "u@example.test", "hash", _tenantId, UserType.Customer);

    [Fact]
    public async Task Lock_RevokesEverySessionAndToken()
    {
        var user = NewUser();
        Mock.Get(_uow.Object.Users).Setup(r => r.GetByIdInTenantAsync(user.Id, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        await UserService().LockAsync(user.Id, _tenantId, CancellationToken.None);

        _sessions.Verify(s => s.RevokeAllForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        user.IsAdministrativelyLocked.Should().BeTrue();
    }

    [Fact]
    public async Task AssignAndRemoveRole_MarkTheUserChanged()
    {
        var tenant = AuthTenant.Create("ACME", "Acme");
        var user = User.Create("u@example.test", "u@example.test", "hash", tenant.Id, UserType.Customer);
        user.AssignToTenant(tenant);
        var role = Role.Create("SALES", "Sales", tenant.Id);
        Mock.Get(_uow.Object.Users).Setup(r => r.GetByIdInTenantAsync(user.Id, tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        Mock.Get(_uow.Object.Roles).Setup(r => r.GetByCodeAsync("SALES", tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        await UserService().AssignRoleAsync(user.Id, "SALES", tenant.Id, null, null, CancellationToken.None);
        await UserService().RemoveRoleAsync(user.Id, role.Id, tenant.Id, CancellationToken.None);

        _revocations.Verify(q => q.UsersChanged(It.Is<Guid[]>(ids => ids.SequenceEqual(new[] { user.Id }))), Times.Exactly(2));
    }

    [Fact]
    public async Task RemovePermissionFromRole_MarksOnlyTheRoleHoldersChanged()
    {
        var role = Role.Create("SALES", "Sales", _tenantId);
        var holders = new[] { Guid.NewGuid(), Guid.NewGuid() };
        Mock.Get(_uow.Object.Roles).Setup(r => r.GetByIdInTenantAsync(role.Id, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        Mock.Get(_uow.Object.Users).Setup(r => r.GetUserIdsByRoleAsync(role.Id, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(holders);

        await new RoleService(_uow.Object).RemovePermissionAsync(role.Id, Guid.NewGuid(), _tenantId, CancellationToken.None);

        _revocations.Verify(q => q.UsersChanged(It.Is<Guid[]>(ids => ids.SequenceEqual(holders))), Times.Once);
    }

    [Fact]
    public async Task DeactivateTenant_MarksTheTenantChanged()
    {
        var tenant = AuthTenant.Create("ACME", "Acme");
        Mock.Get(_uow.Object.Tenants).Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        await new TenantService(_uow.Object).DeactivateAsync(tenant.Id, CancellationToken.None);

        _revocations.Verify(q => q.TenantChanged(tenant.Id), Times.Once);
    }

    [Fact]
    public async Task SessionService_RevokeAllAndBlacklist_ReachTheSharedStore()
    {
        var userId = Guid.NewGuid();
        Mock.Get(_uow.Object.DeviceSessions).Setup(r => r.ListActiveByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DeviceSession>());
        var sessions = new SessionService(_uow.Object);
        var expires = DateTime.UtcNow.AddMinutes(30);

        await sessions.RevokeAllForUserAsync(userId, CancellationToken.None);
        await sessions.BlacklistTokenAsync("jti-1", userId, expires, null, CancellationToken.None);

        _revocations.Verify(q => q.UsersChanged(It.Is<Guid[]>(ids => ids.SequenceEqual(new[] { userId }))), Times.Once);
        _revocations.Verify(q => q.RevokeToken("jti-1", expires), Times.Once);
    }

    [Fact]
    public async Task Queue_PublishesOnFlush_DropsOnClear_AndNeverThrows()
    {
        var store = new InMemoryTokenRevocationStore { Clock = () => 100 };
        var queue = new TokenRevocationQueue(store, NullLogger<TokenRevocationQueue>.Instance);
        var user = Guid.NewGuid();

        queue.UsersChanged(user);
        (await store.GetStateAsync(null, user.ToString(), null)).UserChangedAt.Should().BeNull("nothing before the commit");
        await queue.FlushAsync(CancellationToken.None);
        (await store.GetStateAsync(null, user.ToString(), null)).UserChangedAt.Should().Be(100);

        var other = Guid.NewGuid();
        queue.UsersChanged(other);
        queue.Clear();
        await queue.FlushAsync(CancellationToken.None);
        (await store.GetStateAsync(null, other.ToString(), null)).UserChangedAt.Should().BeNull("rolled back");

        var failing = new Mock<ITokenRevocationStore>();
        failing.Setup(s => s.MarkUsersChangedAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        var failingQueue = new TokenRevocationQueue(failing.Object, NullLogger<TokenRevocationQueue>.Instance);
        failingQueue.UsersChanged(user);
        var act = () => failingQueue.FlushAsync(CancellationToken.None);
        await act.Should().NotThrowAsync("the database change is already committed");
    }

    [Fact]
    public void FailedLoginLockout_IsNotAnAdministrativeLock()
    {
        var user = NewUser();
        for (var i = 0; i < 5; i++)
            user.RecordLoginFail(maxAttempts: 5, lockoutDurationMinutes: 15);

        user.IsCurrentlyLocked.Should().BeTrue();
        user.IsAdministrativelyLocked.Should().BeFalse("a lockout from wrong passwords must not end existing sessions");
    }
}
