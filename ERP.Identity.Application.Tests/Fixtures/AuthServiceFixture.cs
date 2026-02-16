public class AuthServiceFixture
{
    public Mock<IUnitOfWork> UnitOfWorkMock { get; }
    public Mock<IUserRepository> UserRepositoryMock { get; }
    public Mock<IRoleRepository> RoleRepositoryMock { get; }
    public Mock<IPermissionRepository> PermissionRepositoryMock { get; }
    public Mock<IExternalLoginRepository> ExternalLoginRepositoryMock { get; }
    public Mock<IPasswordHasher> PasswordHasherMock { get; }
    public Mock<ITokenService> TokenServiceMock { get; }
    public Mock<IExternalAuthProvider> ExternalAuthProviderMock { get; }

    public AuthServiceFixture()
    {
    }

    public AuthService Create(IEnumerable<IExternalAuthProvider>? providers = null)
        => new AuthService(
            UserRepositoryMock.Object,
            RoleRepositoryMock.Object,
            PermissionRepositoryMock.Object,
            TokenServiceMock.Object,
            PasswordHasherMock.Object,
            UnitOfWorkMock.Object,
            providers ?? Enumerable.Empty<IExternalAuthProvider>()
        );
}
