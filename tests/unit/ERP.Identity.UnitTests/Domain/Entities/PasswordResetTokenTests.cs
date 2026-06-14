using Xunit;
using FluentAssertions;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.UnitTests.Domain.Entities;

public class PasswordResetTokenTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "reset_token_123";
        var expiredAt = DateTime.UtcNow.AddHours(1);

        // Act
        var resetToken = PasswordResetToken.Create(userId, token, expiredAt);

        // Assert
        resetToken.Should().NotBeNull();
        resetToken.UserId.Should().Be(userId);
        resetToken.Token.Should().Be(token);
        resetToken.ExpiredAt.Should().Be(expiredAt);
    }

    [Fact]
    public void Create_ShouldSetUsedAtToNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "reset_token_123";
        var expiredAt = DateTime.UtcNow.AddHours(1);

        // Act
        var resetToken = PasswordResetToken.Create(userId, token, expiredAt);

        // Assert
        resetToken.UsedAt.Should().BeNull();
    }

    [Fact]
    public void IsUsed_WhenTokenNotUsed_ShouldReturnFalse()
    {
        // Arrange
        var resetToken = PasswordResetToken.Create(Guid.NewGuid(), "token", DateTime.UtcNow.AddHours(1));

        // Act & Assert
        resetToken.IsUsed.Should().BeFalse();
    }

    [Fact]
    public void IsUsed_WhenTokenMarkedAsUsed_ShouldReturnTrue()
    {
        // Arrange
        var resetToken = PasswordResetToken.Create(Guid.NewGuid(), "token", DateTime.UtcNow.AddHours(1));

        // Act
        resetToken.MarkUsed();

        // Assert
        resetToken.IsUsed.Should().BeTrue();
    }

    [Fact]
    public void MarkUsed_ShouldSetUsedAtToCurrentTime()
    {
        // Arrange
        var resetToken = PasswordResetToken.Create(Guid.NewGuid(), "token", DateTime.UtcNow.AddHours(1));
        var beforeMark = DateTime.UtcNow;

        // Act
        resetToken.MarkUsed();

        var afterMark = DateTime.UtcNow;

        // Assert
        resetToken.UsedAt.Should().NotBeNull();
        resetToken.UsedAt.Should().BeAfter(beforeMark.AddSeconds(-1));
        resetToken.UsedAt.Should().BeBefore(afterMark.AddSeconds(1));
    }

    [Fact]
    public void IsExpired_WithFutureExpiration_ShouldReturnFalse()
    {
        // Arrange
        var expiredAt = DateTime.UtcNow.AddHours(1);
        var resetToken = PasswordResetToken.Create(Guid.NewGuid(), "token", expiredAt);

        // Act & Assert
        resetToken.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithPastExpiration_ShouldReturnTrue()
    {
        // Arrange
        var expiredAt = DateTime.UtcNow.AddHours(-1);
        var resetToken = PasswordResetToken.Create(Guid.NewGuid(), "token", expiredAt);

        // Act & Assert
        resetToken.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithValidToken_ShouldReturnTrue()
    {
        // Arrange
        var expiredAt = DateTime.UtcNow.AddHours(1);
        var resetToken = PasswordResetToken.Create(Guid.NewGuid(), "token", expiredAt);

        // Act & Assert
        resetToken.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithExpiredToken_ShouldReturnFalse()
    {
        // Arrange
        var expiredAt = DateTime.UtcNow.AddHours(-1);
        var resetToken = PasswordResetToken.Create(Guid.NewGuid(), "token", expiredAt);

        // Act & Assert
        resetToken.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithUsedToken_ShouldReturnFalse()
    {
        // Arrange
        var resetToken = PasswordResetToken.Create(Guid.NewGuid(), "token", DateTime.UtcNow.AddHours(1));
        resetToken.MarkUsed();

        // Act & Assert
        resetToken.IsValid.Should().BeFalse();
    }
}
