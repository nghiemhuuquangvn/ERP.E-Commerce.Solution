using Xunit;
using FluentAssertions;
using Moq;
using ERP.Identity.Application.DTOs.Tenant;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;

namespace ERP.Identity.UnitTests.Application.Services;

public class TenantServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly TenantService _tenantService;

    public TenantServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUnitOfWork.Setup(u => u.Revocations).Returns(new Mock<ERP.Identity.Application.Interfaces.Services.ITokenRevocationQueue>().Object);
        _tenantService = new TenantService(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task ListAllAsync_ShouldReturnAllTenants()
    {
        // Arrange
        var tenants = new List<AuthTenant>
        {
            AuthTenant.Create("tenant1", "Tenant One"),
            AuthTenant.Create("tenant2", "Tenant Two"),
            AuthTenant.Create("tenant3", "Tenant Three")
        };

        var mockTenantRepository = new Mock<ITenantRepository>();
        mockTenantRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenants.AsReadOnly());

        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);

        // Act
        var result = await _tenantService.ListAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(t => t.TenantCode == "TENANT1");
    }

    [Fact]
    public async Task GetByIdAsync_WithValidTenantId_ShouldReturnTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = AuthTenant.Create("tenant1", "Tenant One");
        tenant.GetType().GetProperty("Id")?.SetValue(tenant, tenantId);

        var mockTenantRepository = new Mock<ITenantRepository>();
        mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);

        // Act
        var result = await _tenantService.GetByIdAsync(tenantId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TenantCode.Should().Be("TENANT1");
        result.TenantName.Should().Be("Tenant One");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidTenantId_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        var mockTenantRepository = new Mock<ITenantRepository>();
        mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthTenant?)null);

        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);

        // Act
        var result = await _tenantService.GetByIdAsync(tenantId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreateTenant()
    {
        // Arrange
        var request = new CreateTenantRequest("newtenant", "New Tenant");

        var mockTenantRepository = new Mock<ITenantRepository>();
        mockTenantRepository.Setup(r => r.AddAsync(It.IsAny<AuthTenant>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _tenantService.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TenantCode.Should().Be("NEWTENANT");
        result.TenantName.Should().Be("New Tenant");
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_ShouldUpdateTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = AuthTenant.Create("tenant1", "Old Name");
        tenant.GetType().GetProperty("Id")?.SetValue(tenant, tenantId);

        var request = new UpdateTenantRequest("Updated Name");

        var mockTenantRepository = new Mock<ITenantRepository>();
        mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        mockTenantRepository.Setup(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _tenantService.UpdateAsync(tenantId, request, CancellationToken.None);

        // Assert
        tenant.TenantName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task ActivateAsync_WithInactiveTenant_ShouldActivate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = AuthTenant.Create("tenant1", "Tenant One");
        tenant.Deactivate();
        tenant.GetType().GetProperty("Id")?.SetValue(tenant, tenantId);

        var mockTenantRepository = new Mock<ITenantRepository>();
        mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        mockTenantRepository.Setup(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _tenantService.ActivateAsync(tenantId, CancellationToken.None);

        // Assert
        tenant.IsActive.Should().BeTrue();
        tenant.Status.Should().Be(1);
    }

    [Fact]
    public async Task DeactivateAsync_WithActiveTenant_ShouldDeactivate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = AuthTenant.Create("tenant1", "Tenant One");
        tenant.GetType().GetProperty("Id")?.SetValue(tenant, tenantId);

        var mockTenantRepository = new Mock<ITenantRepository>();
        mockTenantRepository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        mockTenantRepository.Setup(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.Tenants).Returns(mockTenantRepository.Object);
        _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _tenantService.DeactivateAsync(tenantId, CancellationToken.None);

        // Assert
        tenant.IsActive.Should().BeFalse();
        tenant.Status.Should().Be(0);
    }
}
