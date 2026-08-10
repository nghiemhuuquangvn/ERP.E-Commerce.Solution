using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.DTOs.Common;
using ERP.Identity.Application.Interfaces.Services;

namespace ERP.Identity.UnitTests.Api.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _mockUserService = new Mock<IUserService>();
        _controller = new UsersController(_mockUserService.Object);

        var claims = new List<Claim>
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tenant_id", Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task List_WithValidTenantId_ShouldReturnPagedUsers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var users = new PagedResponse<UserDetailDto>(
            new[] { new UserDetailDto(Guid.NewGuid(), "user@example.com", "user", "Test User", true, false, null, new(), new()) },
            1, 1, 10
        );

        _mockUserService.Setup(s => s.ListByTenantAsync(tenantId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _controller.List(tenantId, 1, 10, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithValidUserId_ShouldReturnUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserDetailDto(userId, "user@example.com", "user", "Test User", true, false, null, new(), new());

        _mockUserService.Setup(s => s.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetById(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithInvalidUserId_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockUserService.Setup(s => s.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDetailDto?)null);

        // Act
        var result = await _controller.GetById(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateProfile_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest("New Name", "newusername");

        _mockUserService.Setup(s => s.UpdateProfileAsync(userId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateProfile(userId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new ChangePasswordRequest("oldpassword", "newpassword123");

        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext.HttpContext.User = principal;

        _mockUserService.Setup(s => s.ChangePasswordAsync(userId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ChangePassword(userId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Lock_WithValidUserId_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockUserService.Setup(s => s.LockAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Lock(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Unlock_WithValidUserId_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockUserService.Setup(s => s.UnlockAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Unlock(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delete_WithValidUserId_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockUserService.Setup(s => s.DeactivateAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AssignRole_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new AssignRoleRequest(userId, "admin");

        _mockUserService.Setup(s => s.AssignRoleAsync(userId, "admin", It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AssignRole(userId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RemoveRole_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _mockUserService.Setup(s => s.RemoveRoleAsync(userId, roleId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RemoveRole(userId, roleId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
