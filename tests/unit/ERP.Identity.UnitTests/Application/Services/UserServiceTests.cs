using Xunit;
using FluentAssertions;
using Moq;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.DTOs.Common;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;

namespace ERP.Identity.UnitTests.Application.Services;

public class UserServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _userService = new UserService(_mockUnitOfWork.Object, _mockPasswordHasher.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidUserId_ShouldReturnUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FullName = "Test User",
            IsActive = true
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdWithDetailsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);

        // Act
        var result = await _userService.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidUserId_ShouldReturnNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdWithDetailsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);

        // Act
        var result = await _userService.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListByTenantAsync_ShouldReturnPagedUsers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var users = new List<User>
        {
            new User { Id = Guid.NewGuid(), Email = "user1@example.com", FullName = "User 1" },
            new User { Id = Guid.NewGuid(), Email = "user2@example.com", FullName = "User 2" }
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.ListByTenantAsync(tenantId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.AsReadOnly());
        mockUserRepository.Setup(r => r.CountByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);

        // Act
        var result = await _userService.ListByTenantAsync(tenantId, 1, 10, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task UpdateProfileAsync_WithValidData_ShouldUpdateUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FullName = "Old Name",
            UserName = "oldusername"
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUserRepository.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new UpdateProfileRequest("New Name", "newusername");

        // Act
        await _userService.UpdateProfileAsync(userId, request, CancellationToken.None);

        // Assert
        user.FullName.Should().Be("New Name");
        user.UserName.Should().Be("newusername");
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_ShouldChangePassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentPassword = "oldpassword";
        var newPassword = "newpassword123";
        var user = new User { Id = userId, Email = "test@example.com", PasswordHash = "oldhash" };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUserRepository.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPasswordHasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, currentPassword))
            .Returns(PasswordVerificationResult.Success);
        _mockPasswordHasher.Setup(h => h.HashPassword(user, newPassword))
            .Returns("newhash");

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ChangePasswordRequest(currentPassword, newPassword);

        // Act
        await _userService.ChangePasswordAsync(userId, request, CancellationToken.None);

        // Assert
        user.PasswordHash.Should().Be("newhash");
    }

    [Fact]
    public async Task LockAsync_ShouldLockUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUserRepository.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _userService.LockAsync(userId, CancellationToken.None);

        // Assert - Verify lock was called
        mockUserRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnlockAsync_ShouldUnlockUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUserRepository.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _userService.UnlockAsync(userId, CancellationToken.None);

        // Assert - Verify unlock was called
        mockUserRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockUserRepository.Setup(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _userService.DeactivateAsync(userId, CancellationToken.None);

        // Assert - Verify delete was called
        mockUserRepository.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignRoleAsync_WithValidRole_ShouldAssignRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleCode = "admin";
        var tenantId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com" };
        var role = new Role { Id = Guid.NewGuid(), RoleCode = roleCode, TenantId = tenantId };

        var mockUserRepository = new Mock<IUserRepository>();
        var mockRoleRepository = new Mock<IRoleRepository>();

        mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockRoleRepository.Setup(r => r.GetByCodeAsync(roleCode, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        mockUserRepository.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Users).Returns(mockUserRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.Roles).Returns(mockRoleRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _userService.AssignRoleAsync(userId, roleCode, tenantId, CancellationToken.None);

        // Assert
        mockRoleRepository.Verify(r => r.GetByCodeAsync(roleCode, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
