using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Services;

namespace ERP.Identity.UnitTests.Api.Controllers;

public class RolesControllerTests
{
    private readonly Mock<IRoleService> _mockRoleService;
    private readonly RolesController _controller;

    public RolesControllerTests()
    {
        _mockRoleService = new Mock<IRoleService>();
        _controller = new RolesController(_mockRoleService.Object);

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
    public async Task List_WithValidTenantId_ShouldReturnRoles()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var roles = new List<RoleDto>
        {
            new RoleDto(Guid.NewGuid(), "admin", "Administrator", true),
            new RoleDto(Guid.NewGuid(), "user", "User", true)
        };

        _mockRoleService.Setup(s => s.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles.AsReadOnly());

        // Act
        var result = await _controller.List(tenantId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithValidRoleId_ShouldReturnRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new RoleDto(roleId, "admin", "Administrator", true);

        _mockRoleService.Setup(s => s.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        // Act
        var result = await _controller.GetById(roleId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithInvalidRoleId_ShouldReturnNotFound()
    {
        // Arrange
        var roleId = Guid.NewGuid();

        _mockRoleService.Setup(s => s.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoleDto?)null);

        // Act
        var result = await _controller.GetById(roleId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var request = new CreateRoleRequest("editor", "Editor");
        var createdRole = new RoleDto(Guid.NewGuid(), "editor", "Editor", true);

        _mockRoleService.Setup(s => s.CreateAsync(It.IsAny<Guid>(), request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdRole);

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult?.ActionName.Should().Be(nameof(_controller.GetById));
    }

    [Fact]
    public async Task Update_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest("Updated Editor");

        _mockRoleService.Setup(s => s.UpdateAsync(roleId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Update(roleId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delete_WithValidRoleId_ShouldReturnOk()
    {
        // Arrange
        var roleId = Guid.NewGuid();

        _mockRoleService.Setup(s => s.DeleteAsync(roleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(roleId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPermissions_WithValidRoleId_ShouldReturnPermissions()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissions = new List<string> { "view.users", "edit.users", "delete.users" };

        _mockRoleService.Setup(s => s.GetPermissionsAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions.AsReadOnly());

        // Act
        var result = await _controller.GetPermissions(roleId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AssignPermission_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var request = new AssignPermissionToRoleRequest("view.users");

        _mockRoleService.Setup(s => s.AssignPermissionAsync(roleId, "view.users", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AssignPermission(roleId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RemovePermission_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        _mockRoleService.Setup(s => s.RemovePermissionAsync(roleId, permissionId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RemovePermission(roleId, permissionId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
