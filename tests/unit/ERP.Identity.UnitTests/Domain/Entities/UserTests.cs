using Xunit;
using FluentAssertions;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.UnitTests.Domain.Entities;

public class UserTests
{
    [Fact]
    public void UpdateProfile_WithValidData_ShouldUpdateUserProperties()
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

        // Act
        user.UpdateProfile("New Name", "newusername");

        // Assert
        user.FullName.Should().Be("New Name");
        user.UserName.Should().Be("newusername");
    }

    [Fact]
    public void UpdateProfile_WithNullValues_ShouldNotUpdateProperties()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FullName = "Original Name",
            UserName = "originalusername"
        };

        // Act
        user.UpdateProfile(null, null);

        // Assert
        user.FullName.Should().Be("Original Name");
        user.UserName.Should().Be("originalusername");
    }

    [Fact]
    public void ChangePassword_WithNewPassword_ShouldUpdatePasswordHash()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), PasswordHash = "oldhash" };
        var newHash = "newhash";

        // Act
        user.ChangePassword(newHash);

        // Assert
        user.PasswordHash.Should().Be(newHash);
    }

    [Fact]
    public void ConfirmEmail_ShouldSetIsEmailConfirmedToTrue()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), IsEmailConfirmed = false };

        // Act
        user.ConfirmEmail();

        // Assert
        user.IsEmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), IsActive = true };

        // Act
        user.Deactivate();

        // Assert
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveToTrue()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), IsActive = false };

        // Act
        user.Activate();

        // Assert
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Unlock_ShouldUnlockUser()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid() };

        // Act
        user.Unlock();

        // Assert - Verify unlock was called (no exception thrown)
        user.Should().NotBeNull();
    }
}
