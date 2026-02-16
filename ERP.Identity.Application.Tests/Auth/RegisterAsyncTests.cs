public class RegisterAsyncTests
{
    private readonly AuthServiceFixture _fx = new();

    [Fact]
    public async Task RegisterAsync_NewUser_CreatesUser()
    {
        _fx.UserRepositoryMock.Setup(r => r.ExistsAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _fx.PasswordHasherMock.Setup(h => h.Hash("password"))
            .Returns("HASH");

        var service = _fx.Create();

        await service.RegisterAsync(new RegisterRequest(
            "user@test.com",
            "password",
            Guid.NewGuid()
        ));

        _fx.UserRepositoryMock.Verify(r =>
            r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _fx.UnitOfWorkMock.Verify(u =>
            u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
