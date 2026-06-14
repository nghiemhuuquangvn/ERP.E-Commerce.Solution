using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Services;

namespace ERP.Identity.UnitTests.Api.Controllers;

public class PermissionsControllerTests
{
    private readonly Mock<IPermissionService> _mockPermissionService;
    private readonly PermissionsController _controller;

    public PermissionsControllerTests()
    {
        _mockPermissionService = new Mock<IPermissionService>();
        _controller = new PermissionsController(_mockPermissionService.Object);

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
    public async Task List_ShouldReturnAllPermissions()
    {
        // Arrange
        var permissions = new List<PermissionDto>
        {
            new PermissionDto(Guid.NewGuid(), "view.users", "View Users", "Users"),
            new PermissionDto(Guid.NewGuid(), "edit.users", "Edit Users", "Users"),
            new PermissionDto(Guid.NewGuid(), "delete.users", "Delete Users", "Users")
        };

        _mockPermissionService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions.AsReadOnly());

        // Act
        var result = await _controller.List(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var request = new CreatePermissionRequest("view.reports", "View Reports", "Reports");
        var createdPermission = new PermissionDto(Guid.NewGuid(), "view.reports", "View Reports", "Reports");

        _mockPermissionService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPermission);

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Update_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var permissionId = Guid.NewGuid();
        var request = new UpdatePermissionRequest("Updated View Users", "Users Management");

        _mockPermissionService.Setup(s => s.UpdateAsync(permissionId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Update(permissionId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delete_WithValidPermissionId_ShouldReturnOk()
    {
        // Arrange
        var permissionId = Guid.NewGuid();

        _mockPermissionService.Setup(s => s.DeleteAsync(permissionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(permissionId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
