using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Localization;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.Identity.UnitTests.Application.Services;

public class UserServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<IRoleRepository> _mockRoleRepository = new();
    private readonly Mock<IPasswordHasher> _mockPasswordHasher = new();
    private readonly Mock<ISecurityAuditService> _mockAudit = new();
    private readonly Mock<IPasswordPolicyService> _mockPasswordPolicy = new();
    private readonly Mock<ISessionService> _mockSessionService = new();
    private readonly Mock<IExternalLoginRepository> _mockExternalLogins = new();
    private readonly UserService _userService;
    private readonly Guid _tenantId = Guid.NewGuid();

    public UserServiceTests()
    {
        _mockUnitOfWork.Setup(u => u.Revocations).Returns(new Mock<ERP.Identity.Application.Interfaces.Services.ITokenRevocationQueue>().Object);
        _mockUnitOfWork.Setup(uow => uow.Users).Returns(_mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.Roles).Returns(_mockRoleRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.ExternalLogins).Returns(_mockExternalLogins.Object);
        _mockPasswordHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((hash, provided) => hash == "hashed:" + provided);

        _userService = new UserService(
            _mockUnitOfWork.Object,
            _mockPasswordHasher.Object,
            _mockAudit.Object,
            _mockPasswordPolicy.Object,
            new Mock<IStringLocalizer<AuthMessages>>().Object,
            _mockSessionService.Object,
            new ConfigurationBuilder().Build());
    }

    private static User NewUser(Guid tenantId, string email = "test@example.com") =>
        User.Create(email, email, "hash", tenantId, UserType.Customer);

    [Fact]
    public async Task GetByIdAsync_WithUserInCallerTenant_ShouldReturnUser()
    {
        var user = NewUser(_tenantId);
        _mockUserRepository.Setup(r => r.GetByIdWithDetailsInTenantAsync(user.Id, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _userService.GetByIdAsync(user.Id, _tenantId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithUserOutsideCallerTenant_ShouldReturnNull()
    {
        var userId = Guid.NewGuid();
        _mockUserRepository.Setup(r => r.GetByIdWithDetailsInTenantAsync(userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _userService.GetByIdAsync(userId, _tenantId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_ShouldChangeHashAndRevokeAllSessions()
    {
        var user = NewUser(_tenantId);
        var request = new ChangePasswordRequest("old-password", "New-Password-123!");
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockPasswordHasher.Setup(h => h.VerifyPassword("hash", "old-password")).Returns(true);
        _mockPasswordHasher.Setup(h => h.HashPassword("New-Password-123!")).Returns("new-hash");

        await _userService.ChangePasswordAsync(user.Id, request, CancellationToken.None);

        user.PasswordHash.Should().Be("new-hash");
        _mockSessionService.Verify(s => s.RevokeAllForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ShouldThrowAndKeepSessions()
    {
        var user = NewUser(_tenantId);
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockPasswordHasher.Setup(h => h.VerifyPassword("hash", "wrong")).Returns(false);

        var act = () => _userService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("wrong", "x"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        user.PasswordHash.Should().Be("hash");
        _mockSessionService.Verify(s => s.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LockAsync_WithUserInCallerTenant_ShouldLockUser()
    {
        var user = NewUser(_tenantId);
        _mockUserRepository.Setup(r => r.GetByIdInTenantAsync(user.Id, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        await _userService.LockAsync(user.Id, _tenantId, CancellationToken.None);

        user.IsLocked.Should().BeTrue();
        _mockUserRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateUserAndRevokeAllSessions()
    {
        var user = NewUser(_tenantId);
        _mockUserRepository.Setup(r => r.GetByIdInTenantAsync(user.Id, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        await _userService.DeactivateAsync(user.Id, _tenantId, CancellationToken.None);

        user.IsActive.Should().BeFalse();
        _mockSessionService.Verify(s => s.RevokeAllForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignRoleAsync_WithUserFromAnotherTenant_ShouldThrowNotFoundAndNotAssign()
    {
        // The user exists, but not in the caller's tenant — must look exactly like "not found".
        var foreignUserId = Guid.NewGuid();
        _mockUserRepository.Setup(r => r.GetByIdInTenantAsync(foreignUserId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _userService.AssignRoleAsync(foreignUserId, "ADMIN", _tenantId, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
        _mockUserRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveRoleAsync_WithUserFromAnotherTenant_ShouldThrowNotFound()
    {
        var foreignUserId = Guid.NewGuid();
        _mockUserRepository.Setup(r => r.GetByIdInTenantAsync(foreignUserId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _userService.RemoveRoleAsync(foreignUserId, Guid.NewGuid(), _tenantId, CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    // ---- P0-8: enabling / disabling 2FA re-asks for the password ----

    private User UserWithPassword(string password = "Correct-Password-1")
    {
        var user = User.Create("owner@example.com", "owner", "hashed:" + password, _tenantId, UserType.Customer);
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockExternalLogins.Setup(r => r.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ExternalLogin, bool>>>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalLogin>().AsQueryable());
        return user;
    }

    private void LinkedToGoogle(User user) =>
        _mockExternalLogins.Setup(r => r.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ExternalLogin, bool>>>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalLogin> { ExternalLogin.Create(user.Id, _tenantId, "Google", "g-1", user.Email) }.AsQueryable());

    [Fact]
    public async Task EnableTwoFactor_WithTheRightPassword_Works_AndIsAudited()
    {
        var user = UserWithPassword();

        await _userService.EnableTwoFactorAsync(user.Id, "Email", null, "Correct-Password-1", null, CancellationToken.None);

        user.TwoFactorEnabled.Should().BeTrue();
        _mockAudit.Verify(a => a.LogAsync("TwoFactorEnable", true, _tenantId, user.Id, user.Email, It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData(null)]
    [InlineData("")]
    public async Task EnableTwoFactor_WithAWrongOrMissingPassword_IsRefused_AndNothingChanges(string? password)
    {
        var user = UserWithPassword();

        var act = () => _userService.EnableTwoFactorAsync(user.Id, "Sms", "+84901234567", password, DateTimeOffset.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        user.TwoFactorEnabled.Should().BeFalse();
        user.PhoneNumber.Should().NotBe("+84901234567", "a thief must not be able to point the second factor at their own phone");
    }

    [Fact]
    public async Task DisableTwoFactor_WithAStolenAccessTokenAndNoPassword_IsRefused()
    {
        var user = UserWithPassword();
        user.EnableTwoFactor("Email");

        var act = () => _userService.DisableTwoFactorAsync(user.Id, null, DateTimeOffset.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>().Where(e => e.RuleName == "ReauthenticationRequired");
        user.TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DisableTwoFactor_WithTheRightPassword_Works()
    {
        var user = UserWithPassword();
        user.EnableTwoFactor("Email");

        await _userService.DisableTwoFactorAsync(user.Id, "Correct-Password-1", null, CancellationToken.None);

        user.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DisableTwoFactor_ExternalOnlyAccount_NeedsAFreshToken_NotAPassword()
    {
        var user = UserWithPassword();
        LinkedToGoogle(user);
        user.EnableTwoFactor("Email");

        var stale = () => _userService.DisableTwoFactorAsync(user.Id, null, DateTimeOffset.UtcNow.AddMinutes(-30), CancellationToken.None);
        await stale.Should().ThrowAsync<BusinessRuleException>();
        user.TwoFactorEnabled.Should().BeTrue();

        await _userService.DisableTwoFactorAsync(user.Id, null, DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None);
        user.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DisableTwoFactor_AFreshTokenAloneDoesNotCountForAnAccountWithAPassword()
    {
        var user = UserWithPassword();
        user.EnableTwoFactor("Email");

        var act = () => _userService.DisableTwoFactorAsync(user.Id, null, DateTimeOffset.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}
