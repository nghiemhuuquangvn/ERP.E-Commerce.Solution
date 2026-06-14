using Xunit;
using FluentAssertions;
using Moq;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;

namespace ERP.Identity.UnitTests.Application.Services;

public class RoleServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly RoleService _roleService;

    public RoleServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _roleService = new RoleService(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task ListByTenantAsync_ShouldReturnRoles()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var roles = new List<Role>
        {
            new Role { Id = Guid.NewGuid(), RoleCode = "admin", RoleName = "Administrator", TenantId = tenantId },
            new Role { Id = Guid.NewGuid(), RoleCode = "user", RoleName = "User", TenantId = tenantId }
        };

        var mockRoleRepository = new Mock<IRoleRepository>();
        mockRoleRepository.Setup(r => r.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles.AsReadOnly());

        _mockUnitOfWork.Setup(uow => uow.Roles).Returns(mockRoleRepository.Object);

        // Act
        var result = await _roleService.ListByTenantAsync(tenantId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.RoleCode == "admin");
        result.Should().Contain(r => r.RoleCode == "user");
    }

    [Fact]
    public async Task GetByIdAsync_WithValidRoleId_ShouldReturnRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role
        {
            Id = roleId,
            RoleCode = "admin",
            RoleName = "Administrator",
            TenantId = Guid.NewGuid()
        };

        var mockRoleRepository = new Mock<IRoleRepository>();
        mockRoleRepository.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        _mockUnitOfWork.Setup(uow => uow.Roles).Returns(mockRoleRepository.Object);

        // Act
        var result = await _roleService.GetByIdAsync(roleId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RoleCode.Should().Be("admin");
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreateRole()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new CreateRoleRequest("admin", "Administrator");

        var mockRoleRepository = new Mock<IRoleRepository>();
        mockRoleRepository.Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Roles).Returns(mockRoleRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _roleService.CreateAsync(tenantId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RoleCode.Should().Be("admin");
        result.RoleName.Should().Be("Administrator");
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_ShouldUpdateRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role
        {
            Id = roleId,
            RoleCode = "admin",
            RoleName = "Old Name",
            TenantId = Guid.NewGuid()
        };
        var request = new UpdateRoleRequest("New Administrator Name");

        var mockRoleRepository = new Mock<IRoleRepository>();
        mockRoleRepository.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        mockRoleRepository.Setup(r => r.UpdateAsync(role, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Roles).Returns(mockRoleRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _roleService.UpdateAsync(roleId, request, CancellationToken.None);

        // Assert
        role.RoleName.Should().Be("New Administrator Name");
    }

    [Fact]
    public async Task DeleteAsync_WithValidRoleId_ShouldDeleteRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role { Id = roleId, RoleCode = "admin", TenantId = Guid.NewGuid() };

        var mockRoleRepository = new Mock<IRoleRepository>();
        mockRoleRepository.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        mockRoleRepository.Setup(r => r.DeleteAsync(role, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Roles).Returns(mockRoleRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _roleService.DeleteAsync(roleId, CancellationToken.None);

        // Assert
        mockRoleRepository.Verify(r => r.DeleteAsync(role, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPermissionsAsync_ShouldReturnPermissions()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissions = new List<string> { "view.users", "edit.users", "delete.users" };

        var mockPermissionRepository = new Mock<IPermissionRepository>();
        mockPermissionRepository.Setup(r => r.GetPermissionsForRoleAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions.AsReadOnly());

        _mockUnitOfWork.Setup(uow => uow.Permissions).Returns(mockPermissionRepository.Object);

        // Act
        var result = await _roleService.GetPermissionsAsync(roleId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("view.users");
    }

    [Fact]
    public async Task AssignPermissionAsync_WithValidPermission_ShouldAssignPermissionToRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionCode = "view.users";
        var tenantId = Guid.NewGuid();
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            PermissionCode = permissionCode,
            PermissionName = "View Users"
        };

        var mockPermissionRepository = new Mock<IPermissionRepository>();
        mockPermissionRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { permission }.AsReadOnly());
        mockPermissionRepository.Setup(r => r.AssignToRoleAsync(roleId, permission.Id, tenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Permissions).Returns(mockPermissionRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _roleService.AssignPermissionAsync(roleId, permissionCode, tenantId, CancellationToken.None);

        // Assert
        mockPermissionRepository.Verify(r => r.AssignToRoleAsync(roleId, permission.Id, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovePermissionAsync_WithValidPermission_ShouldRemovePermissionFromRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var mockPermissionRepository = new Mock<IPermissionRepository>();
        mockPermissionRepository.Setup(r => r.RemoveFromRoleAsync(roleId, permissionId, tenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Permissions).Returns(mockPermissionRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _roleService.RemovePermissionAsync(roleId, permissionId, tenantId, CancellationToken.None);

        // Assert
        mockPermissionRepository.Verify(r => r.RemoveFromRoleAsync(roleId, permissionId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
