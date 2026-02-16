public class LoginAsyncTests
{
    private readonly AuthServiceFixture _fx = new();

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        var user = User.Create("user@test.com", "HASH");
        var tenant = new AuthTenant(Guid.NewGuid(), "TENANT");

        _fx.UserRepositoryMock.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _fx.PasswordHasherMock.Setup(h => h.Verify("password", "HASH"))
            .Returns(true);

        _fx.TenantRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _fx.RoleRepositoryMock.Setup(r => r.GetRolesAsync(user.Id, tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "User" });

        _fx.PermissionRepositoryMock.Setup(r => r.GetPermissionsAsync(user.Id, tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "read" });

        _fx.TokenServiceMock.Setup(t =>
                t.GenerateAccessToken(user, tenant,
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync("ACCESS");

        _fx.TokenServiceMock.Setup(t =>
                t.GenerateRefreshToken(user.Id, tenant.Id))
            .ReturnsAsync("REFRESH");

        var service = _fx.Create();

        var result = await service.LoginAsync(
            new LoginRequest(user.Email, "password", tenant.Id));

        result.AccessToken.Should().Be("ACCESS");
        result.RefreshToken.Should().Be("REFRESH");
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ThrowsUnauthorized()
    {
        var user = User.Create("user@test.com", "HASH");

        _fx.UserRepositoryMock.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _fx.PasswordHasherMock.Setup(h => h.Verify("wrong", "HASH"))
            .Returns(false);

        var service = _fx.Create();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest(user.Email, "wrong", Guid.NewGuid()))
        );
    }
    [Fact]
    public async Task LoginAsync_ShouldThrow_WhenPasswordInvalid()
    {
        // Arrange
        var fixture = new AuthServiceFixture();
        var request = new LoginRequest("test@mail.com", "wrong", tenantId);

        fixture.UserRepository
            .Setup(x => x.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserBuilder.ActiveUser().WithPassword("hashed"));

        fixture.PasswordHasher
            .Setup(x => x.Verify("wrong", It.IsAny<string>()))
            .Returns(false);

        // Act
        Func<Task> act = () => fixture.AuthService.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
