using Xunit;
using FluentAssertions;
using Moq;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;

namespace ERP.Identity.UnitTests.Application.Services;

public class PermissionServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPermissionRepository> _mockPermissionRepository;
    private readonly PermissionService _permissionService;

    public PermissionServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPermissionRepository = new Mock<IPermissionRepository>();
        _mockUnitOfWork.Setup(uow => uow.Permissions).Returns(_mockPermissionRepository.Object);
        _permissionService = new PermissionService(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task ListAllAsync_ShouldReturnAllPermissions()
    {
        // Arrange
        var permissions = new List<Permission>
        {
            Permission.Create("view.users", "View Users", "Users"),
            Permission.Create("edit.users", "Edit Users", "Users"),
            Permission.Create("delete.users", "Delete Users", "Users")
        };

        _mockPermissionRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions.AsReadOnly());

        // Act
        var result = await _permissionService.ListAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(p => p.PermissionCode == "view.users");
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreatePermission()
    {
        // Arrange
        var request = new CreatePermissionRequest("view.reports", "View Reports", "Reports");

        _mockPermissionRepository.Setup(r => r.AddAsync(It.IsAny<Permission>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _permissionService.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PermissionCode.Should().Be("view.reports");
        result.PermissionName.Should().Be("View Reports");
        _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_ShouldUpdatePermission()
    {
        // Arrange
        var permissionId = Guid.NewGuid();
        var permission = Permission.Create("view.users", "View Users", "Users");
        permission.GetType().GetProperty("Id")?.SetValue(permission, permissionId);

        var request = new UpdatePermissionRequest("View All Users", "Users Management");

        _mockPermissionRepository.Setup(r => r.GetByIdAsync(permissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);
        _mockPermissionRepository.Setup(r => r.UpdateAsync(permission, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _permissionService.UpdateAsync(permissionId, request, CancellationToken.None);

        // Assert
        permission.PermissionName.Should().Be("View All Users");
        permission.Module.Should().Be("Users Management");
        _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithValidPermissionId_ShouldDeletePermission()
    {
        // Arrange
        var permissionId = Guid.NewGuid();
        var permission = Permission.Create("view.users", "View Users", "Users");
        permission.GetType().GetProperty("Id")?.SetValue(permission, permissionId);

        _mockPermissionRepository.Setup(r => r.GetByIdAsync(permissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);
        _mockPermissionRepository.Setup(r => r.DeleteAsync(permission, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _permissionService.DeleteAsync(permissionId, CancellationToken.None);

        // Assert
        _mockPermissionRepository.Verify(r => r.DeleteAsync(permission, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
