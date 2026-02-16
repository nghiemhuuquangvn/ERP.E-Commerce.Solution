public class RefreshTokenTests
{
    private readonly AuthServiceFixture _fx = new();

    [Fact]
    public async Task RefreshToken_Valid_ReturnsNewAccessToken()
    {
        var user = User.Create("a@b.com", "HASH");
        var tenant = new AuthTenant(Guid.NewGuid(), "TENANT");

        _fx.UserRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _fx.TenantRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _fx.TokenServiceMock.Setup(t =>
                t.GenerateAccessToken(
                    user,
                    tenant,
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync("NEW_ACCESS");

        var service = _fx.Create();

        var result = await service.RefreshTokenAsync(
            new RefreshTokenRequest("REFRESH"));

        result.AccessToken.Should().Be("NEW_ACCESS");
    }
}
