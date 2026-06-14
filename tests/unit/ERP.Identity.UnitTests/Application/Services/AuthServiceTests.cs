using Xunit;
using FluentAssertions;
using Moq;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Interfaces.External;
using ERP.Identity.Application.Interfaces.Mapping;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;

namespace ERP.Identity.UnitTests.Application.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<IEnumerable<IExternalAuthProvider>> _mockExternalAuthProviders;
    private readonly Mock<IUserRoleResolver> _mockUserRoleResolver;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockTokenService = new Mock<ITokenService>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockExternalAuthProviders = new Mock<IEnumerable<IExternalAuthProvider>>();
        _mockUserRoleResolver = new Mock<IUserRoleResolver>();

        _authService = new AuthService(
            _mockUnitOfWork.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockExternalAuthProviders.Object,
            _mockUserRoleResolver.Object
        );
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithValidEmail_ShouldCreatePasswordResetToken()
    {
        // Arrange
        var email = "test@example.com";
        var tenantCode = "tenant001";
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = email };
        var tenant = AuthTenant.Create(tenantCode, "Test Tenant");

        var mockUserRepository = new Mock<IUserRepository>();
        var mockTenantRepository = new Mock<ITenantRepository>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();

        mockUserRepository.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockTenantRepository.Setup(r => r.GetByCodeAsync(tenantCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        mockPasswordResetTokenRepository.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.PasswordResetTokens).Returns(mockPasswordResetTokenRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ForgotPasswordRequest(email, tenantCode);

        // Act
        var result = await _authService.ForgotPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.ResetToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithInvalidEmail_ShouldReturnFailure()
    {
        // Arrange
        var email = "invalid@example.com";
        var tenantCode = "tenant001";

        var mockUserRepository = new Mock<IUserRepository>();
        var mockTenantRepository = new Mock<ITenantRepository>();

        mockUserRepository.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);

        var request = new ForgotPasswordRequest(email, tenantCode);

        // Act
        var result = await _authService.ForgotPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidToken_ShouldResetPassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com", PasswordHash = "oldhash" };
        var token = "valid_token";
        var newPassword = "newpassword123";
        var newHash = "newhash";
        var expiredAt = DateTime.UtcNow.AddHours(1);

        var resetToken = PasswordResetToken.Create(userId, token, expiredAt);

        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockUserRepository = new Mock<IUserRepository>();

        mockPasswordResetTokenRepository.Setup(r => r.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);
        mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockPasswordHasher.Setup(h => h.HashPassword(user, newPassword))
            .Returns(newHash);

        _mockUnitOfWork.Setup(uow => uow.PasswordResetTokens).Returns(mockPasswordResetTokenRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ResetPasswordRequest(token, newPassword);

        // Act
        var result = await _authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredToken_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "expired_token";
        var expiredAt = DateTime.UtcNow.AddHours(-1);

        var resetToken = PasswordResetToken.Create(userId, token, expiredAt);

        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();

        mockPasswordResetTokenRepository.Setup(r => r.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        _mockUnitOfWork.Setup(uow => uow.PasswordResetTokens).Returns(mockPasswordResetTokenRepository.Object);

        var request = new ResetPasswordRequest(token, "newpassword");

        // Act
        var result = await _authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_WithValidRefreshToken_ShouldRevokeToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshToken = "valid_refresh_token";

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();

        mockRefreshTokenRepository.Setup(r => r.RevokeAllByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.RefreshTokens).Returns(mockRefreshTokenRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _authService.LogoutAsync(userId, refreshToken, CancellationToken.None);

        // Assert
        mockRefreshTokenRepository.Verify(r => r.RevokeAllByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithValidUserId_ShouldConfirmEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com", IsEmailConfirmed = false };
        var token = "confirm_token";

        var mockUserRepository = new Mock<IUserRepository>();

        mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ConfirmEmailRequest(userId.ToString(), token);

        // Act
        var result = await _authService.ConfirmEmailAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        user.IsEmailConfirmed.Should().BeTrue();
    }
}
