using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Tenant;
using ERP.Identity.Application.Interfaces.Services;

namespace ERP.Identity.UnitTests.Api.Controllers;

public class TenantsControllerTests
{
    private readonly Mock<ITenantService> _mockTenantService;
    private readonly TenantsController _controller;

    public TenantsControllerTests()
    {
        _mockTenantService = new Mock<ITenantService>();
        _controller = new TenantsController(_mockTenantService.Object);

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
    public async Task List_ShouldReturnAllTenants()
    {
        // Arrange
        var tenants = new List<TenantDto>
        {
            new TenantDto(Guid.NewGuid(), "tenant1", "Tenant One", 1),
            new TenantDto(Guid.NewGuid(), "tenant2", "Tenant Two", 1),
            new TenantDto(Guid.NewGuid(), "tenant3", "Tenant Three", 0)
        };

        _mockTenantService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenants.AsReadOnly());

        // Act
        var result = await _controller.List(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithValidTenantId_ShouldReturnTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new TenantDto(tenantId, "tenant1", "Tenant One", 1);

        _mockTenantService.Setup(s => s.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _controller.GetById(tenantId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithInvalidTenantId_ShouldReturnNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockTenantService.Setup(s => s.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantDto?)null);

        // Act
        var result = await _controller.GetById(tenantId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var request = new CreateTenantRequest("newtenant", "New Tenant");
        var createdTenant = new TenantDto(Guid.NewGuid(), "newtenant", "New Tenant", 1);

        _mockTenantService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTenant);

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
        var tenantId = Guid.NewGuid();
        var request = new UpdateTenantRequest("Updated Tenant Name");

        _mockTenantService.Setup(s => s.UpdateAsync(tenantId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Update(tenantId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Activate_WithInactiveTenant_ShouldReturnOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockTenantService.Setup(s => s.ActivateAsync(tenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Activate(tenantId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Deactivate_WithActiveTenant_ShouldReturnOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockTenantService.Setup(s => s.DeactivateAsync(tenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Deactivate(tenantId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
