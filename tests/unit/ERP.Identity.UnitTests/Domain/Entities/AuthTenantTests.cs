using Xunit;
using FluentAssertions;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.UnitTests.Domain.Entities;

public class AuthTenantTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateTenant()
    {
        // Arrange
        var code = "tenant001";
        var name = "Test Tenant";

        // Act
        var tenant = AuthTenant.Create(code, name);

        // Assert
        tenant.Should().NotBeNull();
        tenant.TenantCode.Should().Be(code.ToUpperInvariant());
        tenant.TenantName.Should().Be(name);
    }

    [Fact]
    public void Create_ShouldHaveActiveStatus()
    {
        // Arrange
        var code = "tenant001";
        var name = "Test Tenant";

        // Act
        var tenant = AuthTenant.Create(code, name);

        // Assert
        tenant.Status.Should().Be(1); // Active status
    }

    [Fact]
    public void Create_ShouldHaveUniqueId()
    {
        // Arrange & Act
        var tenant1 = AuthTenant.Create("t1", "Tenant 1");
        var tenant2 = AuthTenant.Create("t2", "Tenant 2");

        // Assert
        tenant1.Id.Should().NotBe(tenant2.Id);
    }

    [Fact]
    public void Activate_WithInactiveTenant_ShouldSetStatusToActive()
    {
        // Arrange
        var tenant = AuthTenant.Create("tenant001", "Test Tenant");
        tenant.Deactivate();

        // Act
        tenant.Activate();

        // Assert
        tenant.Status.Should().Be(1);
    }

    [Fact]
    public void Deactivate_WithActiveTenant_ShouldSetStatusToInactive()
    {
        // Arrange
        var tenant = AuthTenant.Create("tenant001", "Test Tenant");

        // Act
        tenant.Deactivate();

        // Assert
        tenant.Status.Should().Be(0);
    }

    [Fact]
    public void IsActive_WithStatusOne_ShouldReturnTrue()
    {
        // Arrange
        var tenant = AuthTenant.Create("tenant001", "Test Tenant");

        // Act & Assert
        tenant.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WithStatusZero_ShouldReturnFalse()
    {
        // Arrange
        var tenant = AuthTenant.Create("tenant001", "Test Tenant");
        tenant.Deactivate();

        // Act & Assert
        tenant.IsActive.Should().BeFalse();
    }
}
