public class ExternalLoginAsyncTests
{
    private readonly AuthServiceFixture _fx = new();

    [Fact]
    public async Task ExternalLogin_NewUser_AutoRegisters()
    {
        var provider = new Mock<IExternalAuthProvider>();

        provider.Setup(p => p.Provider).Returns("google");
        provider.Setup(p => p.VerifyAsync(
                It.IsAny<ExternalLoginRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalUserInfo(
                "external-id",
                "user@gmail.com"
            ));

        var service = _fx.Create(new[] { provider.Object });

        await service.ExternalLoginAsync(new ExternalLoginRequest(
            "google",
            "id-token",
            Guid.NewGuid()
        ));

        _fx.UserRepo.Verify(r =>
            r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
