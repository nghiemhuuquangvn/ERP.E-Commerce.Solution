using Xunit;
using FluentAssertions;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.UnitTests.Domain.Entities;

public class PermissionTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreatePermission()
    {
        // Arrange
        var code = "view.users";
        var name = "View Users";
        var module = "Users";

        // Act
        var permission = Permission.Create(code, name, module);

        // Assert
        permission.Should().NotBeNull();
        permission.PermissionCode.Should().Be(code);
        permission.PermissionName.Should().Be(name);
        permission.Module.Should().Be(module);
    }

    [Fact]
    public void Create_ShouldHaveUniqueId()
    {
        // Arrange & Act
        var permission1 = Permission.Create("perm1", "Permission 1", "Module");
        var permission2 = Permission.Create("perm2", "Permission 2", "Module");

        // Assert
        permission1.Id.Should().NotBe(permission2.Id);
    }

    [Fact]
    public void Create_ShouldSetCreatedAtTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var permission = Permission.Create("test", "Test", "Module");

        var afterCreation = DateTime.UtcNow;

        // Assert
        permission.CreatedAt.Should().BeAfter(beforeCreation.AddSeconds(-1));
        permission.CreatedAt.Should().BeBefore(afterCreation.AddSeconds(1));
    }
}
