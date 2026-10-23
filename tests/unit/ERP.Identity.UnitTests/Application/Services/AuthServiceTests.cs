using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.External;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Constants;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Domain.Events;
using ERP.Identity.Infrastructure.ExternalAuthLogin;
using ERP.Identity.Infrastructure.Mapping;
using ERP.Identity.Infrastructure.Security;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.Identity.UnitTests.Application.Services;

/// <summary>
/// Security regression tests for AuthService (2026-10-19 IdentityService security review):
/// self-registration/external-login privilege escalation, forgot-password token exposure,
/// token-less email confirmation, deactivated accounts still authenticating, and sessions
/// surviving a password reset.
/// </summary>
public class AuthServiceTests
{
    private const string TenantCode = "ACME";

    private readonly Mock<IUnitOfWork> _uow = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<IPasswordResetTokenRepository> _resetTokens = new();
    private readonly Mock<IEmailConfirmTokenRepository> _confirmTokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ILoginAttemptRepository> _loginAttempts = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly Mock<ISecurityAuditService> _audit = new();
    private readonly Mock<ISessionService> _sessions = new();
    private readonly Mock<IPasswordPolicyService> _passwordPolicy = new();
    private readonly Mock<ILicenseService> _license = new();
    private readonly Mock<IExternalAuthProvider> _googleProvider = new();
    private readonly Mock<IExternalLoginRepository> _externalLogins = new();
    private readonly Mock<ITwoFactorChallengeRepository> _twoFactorChallenges = new();
    private readonly AuthTenant _tenant = AuthTenant.Create(TenantCode, "Acme Corp");

    public AuthServiceTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _uow.Setup(u => u.Roles).Returns(_roles.Object);
        _uow.Setup(u => u.Tenants).Returns(_tenants.Object);
        _uow.Setup(u => u.PasswordResetTokens).Returns(_resetTokens.Object);
        _uow.Setup(u => u.EmailConfirmTokens).Returns(_confirmTokens.Object);
        _uow.Setup(u => u.RefreshTokens).Returns(_refreshTokens.Object);
        _uow.Setup(u => u.LoginAttempts).Returns(_loginAttempts.Object);
        _uow.Setup(u => u.ExternalLogins).Returns(_externalLogins.Object);
        _uow.Setup(u => u.TwoFactorChallenges).Returns(_twoFactorChallenges.Object);

        _tenants.Setup(t => t.GetTenantByCodeAsync(TenantCode, It.IsAny<CancellationToken>())).ReturnsAsync(_tenant);
        _tenants.Setup(t => t.GetByIdAsync(_tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_tenant);

        _roles.Setup(r => r.GetRolesByCodeAsync(It.IsAny<IEnumerable<string>>(), _tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> codes, Guid tenantId, CancellationToken _) =>
                codes.Select(code => Role.Create(code, code, tenantId)).ToList());
        _roles.Setup(r => r.GetRolesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<AuthTenant>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync("access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync("refresh-token");

        _passwordHasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => "hashed:" + p);
        _passwordHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((hash, provided) => hash == "hashed:" + provided);

        _settings.Setup(s => s.GetOrCreateSystemSettingsEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemAuthSettings.CreateDefault());
        _settings.Setup(s => s.GetOrCreateTenantSettingsEntityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => TenantAuthSettings.CreateDefault(id));
        _settings.Setup(s => s.IsProviderEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _googleProvider.SetupGet(p => p.Provider).Returns("Google");
    }

    private AuthService CreateService(Dictionary<string, string?>? config = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? new Dictionary<string, string?>())
            .Build();

        return new AuthService(
            _uow.Object,
            _tokenService.Object,
            _passwordHasher.Object,
            new[] { _googleProvider.Object },
            new UserRoleResolver(),
            _settings.Object,
            new TwoFactorCodeGenerator(),
            new TwitterOAuthService(new Mock<IHttpClientFactory>().Object, configuration, _uow.Object),
            configuration,
            _audit.Object,
            _sessions.Object,
            _passwordPolicy.Object,
            _license.Object);
    }

    private User ExistingUser(string password = "Correct-Password-1", bool active = true)
    {
        var user = User.Create("victim@acme.test", "victim", "hashed:" + password, _tenant.Id, UserType.Admin);
        if (!active)
            user.Deactivate();
        _users.Setup(u => u.GetByEmailAsync(user.Email, _tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(u => u.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    // ---- C1: privilege escalation via self-service account creation ----

    [Theory]
    [InlineData(UserType.Admin)]
    [InlineData(UserType.SuperAdmin)]
    [InlineData(UserType.System)]
    public async Task RegisterAsync_IgnoresClientSuppliedPrivilegedUserType_AndAssignsCustomerRoleOnly(UserType requested)
    {
        User? created = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => created = u);
        var request = new RegisterDto.RegisterRequest("attacker@evil.test", null, "Str0ng-Passw0rd!", TenantCode, requested);

        await CreateService().RegisterAsync(request, CancellationToken.None);

        created.Should().NotBeNull();
        created!.UserTypeCode.Should().Be(nameof(UserType.Customer));
        _roles.Verify(r => r.GetRolesByCodeAsync(
            It.Is<IEnumerable<string>>(codes => codes.SequenceEqual(new[] { SystemRoles.Customer })),
            _tenant.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- C2: forgot-password must never hand the reset token to the caller ----

    [Fact]
    public async Task ForgotPasswordAsync_DoesNotReturnToken_AndQueuesResetEmailWithHashedTokenOnly()
    {
        var user = ExistingUser();
        PasswordResetToken? stored = null;
        _resetTokens.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((t, _) => stored = t);

        var response = await CreateService().ForgotPasswordAsync(new ForgotPasswordRequest(user.Email, TenantCode), CancellationToken.None);

        response.Succeeded.Should().BeTrue();
        response.ResetToken.Should().BeNull();

        stored.Should().NotBeNull();
        var evt = stored!.DomainEvents.OfType<PasswordResetRequestedDomainEvent>().Single();
        evt.Email.Should().Be(user.Email);
        var rawToken = Uri.UnescapeDataString(evt.ResetLink.Split("token=")[1]);
        stored.Token.Should().Be(OpaqueTokenHasher.Hash(rawToken)).And.NotBe(rawToken);
        stored.ExpiredAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnknownTenantOrEmail_ReturnsSameSuccessWithoutCreatingToken()
    {
        _tenants.Setup(t => t.GetTenantByCodeAsync("NOPE", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Not found the tenant"));
        var service = CreateService();

        var unknownTenant = await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@b.test", "NOPE"), CancellationToken.None);
        var unknownEmail = await service.ForgotPasswordAsync(new ForgotPasswordRequest("ghost@acme.test", TenantCode), CancellationToken.None);

        unknownTenant.Should().BeEquivalentTo(new ForgotPasswordResponse(true));
        unknownEmail.Should().BeEquivalentTo(new ForgotPasswordResponse(true));
        _resetTokens.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_TestingFlagEnabled_ReturnsToken()
    {
        var user = ExistingUser();
        var service = CreateService(new() { ["Auth:ExposeTokensInResponseForTesting"] = "true" });

        var response = await service.ForgotPasswordAsync(new ForgotPasswordRequest(user.Email, TenantCode), CancellationToken.None);

        response.ResetToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ChangesPasswordAndRevokesEverySession()
    {
        var user = ExistingUser();
        const string rawToken = "raw-reset-token";
        var stored = PasswordResetToken.Create(user.Id, OpaqueTokenHasher.Hash(rawToken), DateTime.UtcNow.AddMinutes(30));
        _resetTokens.Setup(r => r.GetByTokenAsync(OpaqueTokenHasher.Hash(rawToken), It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        await CreateService().ResetPasswordAsync(new ResetPasswordRequest(rawToken, "Brand-New-Pass-1"), CancellationToken.None);

        user.PasswordHash.Should().Be("hashed:Brand-New-Pass-1");
        stored.IsUsed.Should().BeTrue();
        _sessions.Verify(s => s.RevokeAllForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownToken_IsRejected()
    {
        var act = () => CreateService().ResetPasswordAsync(new ResetPasswordRequest("guessed", "Whatever-1"), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // ---- H1: email confirmation requires the emailed token ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-token")]
    public async Task ConfirmEmailAsync_WithoutValidToken_IsRejected(string? token)
    {
        var user = ExistingUser();

        var act = () => CreateService().ConfirmEmailAsync(new ConfirmEmailRequest(user.Id.ToString(), token!), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        user.IsEmailConfirmed.Should().NotBe(true);
    }

    [Fact]
    public async Task ConfirmEmailAsync_TokenIssuedForAnotherUser_IsRejected()
    {
        var user = ExistingUser();
        const string rawToken = "someone-elses-token";
        _confirmTokens.Setup(r => r.GetByTokenAsync(OpaqueTokenHasher.Hash(rawToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailConfirmToken.Create(Guid.NewGuid(), OpaqueTokenHasher.Hash(rawToken), DateTime.UtcNow.AddHours(1)));

        var act = () => CreateService().ConfirmEmailAsync(new ConfirmEmailRequest(user.Id.ToString(), rawToken), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        user.IsEmailConfirmed.Should().NotBe(true);
    }

    [Fact]
    public async Task ConfirmEmailAsync_ValidToken_ConfirmsEmail()
    {
        var user = ExistingUser();
        const string rawToken = "valid-confirm-token";
        var stored = EmailConfirmToken.Create(user.Id, OpaqueTokenHasher.Hash(rawToken), DateTime.UtcNow.AddHours(1));
        _confirmTokens.Setup(r => r.GetByTokenAsync(OpaqueTokenHasher.Hash(rawToken), It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        await CreateService().ConfirmEmailAsync(new ConfirmEmailRequest(user.Id.ToString(), rawToken), CancellationToken.None);

        user.IsEmailConfirmed.Should().BeTrue();
        stored.IsConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_IssuesHashedEmailConfirmationToken()
    {
        EmailConfirmToken? stored = null;
        _confirmTokens.Setup(r => r.AddAsync(It.IsAny<EmailConfirmToken>(), It.IsAny<CancellationToken>()))
            .Callback<EmailConfirmToken, CancellationToken>((t, _) => stored = t);

        await CreateService().RegisterAsync(
            new RegisterDto.RegisterRequest("new@acme.test", null, "Str0ng-Passw0rd!", TenantCode, UserType.Customer), CancellationToken.None);

        var evt = stored!.DomainEvents.OfType<EmailConfirmationRequestedDomainEvent>().Single();
        var rawToken = Uri.UnescapeDataString(evt.ConfirmLink.Split("token=")[1]);
        stored.Token.Should().Be(OpaqueTokenHasher.Hash(rawToken));
    }

    // ---- H3: deactivated accounts must not authenticate ----

    [Fact]
    public async Task LoginAsync_DeactivatedUserWithCorrectPassword_IsRejectedWithoutIssuingTokens()
    {
        var user = ExistingUser(active: false);

        var act = () => CreateService().LoginAsync(
            new LoginDto.LoginRequest(user.Email, "Correct-Password-1", UserType.Admin), TenantCode, "1.2.3.4", "ua", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tokenService.Verify(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<AuthTenant>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_UserOfDeactivatedTenant_IsRejected()
    {
        var user = ExistingUser();
        _tenant.Deactivate();

        var act = () => CreateService().LoginAsync(
            new LoginDto.LoginRequest(user.Email, "Correct-Password-1", UserType.Admin), TenantCode, "1.2.3.4", "ua", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RefreshTokenAsync_UserDeactivatedAfterLogin_IsRejected()
    {
        var user = ExistingUser(active: false);
        const string rawRefresh = "still-valid-refresh-token";
        var session = RefreshToken.Create(user.Id, _tenant.Id, OpaqueTokenHasher.Hash(rawRefresh), DateTime.UtcNow.AddDays(1));
        _refreshTokens.Setup(r => r.GetByTokenAsync(OpaqueTokenHasher.Hash(rawRefresh), It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var act = () => CreateService().RefreshTokenAsync(new RefreshTokenDto.RefreshTokenRequest(rawRefresh, _tenant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tokenService.Verify(t => t.GenerateRefreshToken(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    // ---- H2: interactive OIDC login uses the same protections ----

    [Fact]
    public async Task VerifyInteractiveCredentialsAsync_WrongPassword_CountsFailureAgainstAccount()
    {
        var user = ExistingUser();
        _users.Setup(u => u.FindAllByEmailGloballyAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(new List<User> { user });

        var result = await CreateService().VerifyInteractiveCredentialsAsync(user.Email, "wrong", "1.2.3.4", "ua", CancellationToken.None);

        result.Should().BeNull();
        user.AccessFailedCount.Should().Be(1);
        _audit.Verify(a => a.RecordLoginAttemptAsync("1.2.3.4", false, user.Email, null, "Invalid credentials", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyInteractiveCredentialsAsync_DeactivatedUser_IsRejectedEvenWithCorrectPassword()
    {
        var user = ExistingUser(active: false);
        _users.Setup(u => u.FindAllByEmailGloballyAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(new List<User> { user });

        var result = await CreateService().VerifyInteractiveCredentialsAsync(user.Email, "Correct-Password-1", "1.2.3.4", "ua", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyInteractiveCredentialsAsync_TooManyRecentFailures_IsRejectedWithoutCheckingPassword()
    {
        var user = ExistingUser();
        _loginAttempts.Setup(l => l.CountRecentFailedAsync("1.2.3.4", user.Email, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);

        var result = await CreateService().VerifyInteractiveCredentialsAsync(user.Email, "Correct-Password-1", "1.2.3.4", "ua", CancellationToken.None);

        result.Should().BeNull();
        _users.Verify(u => u.FindAllByEmailGloballyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyInteractiveCredentialsAsync_CorrectPassword_ReturnsUserId()
    {
        var user = ExistingUser();
        _users.Setup(u => u.FindAllByEmailGloballyAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(new List<User> { user });

        var result = await CreateService().VerifyInteractiveCredentialsAsync(user.Email, "Correct-Password-1", "1.2.3.4", "ua", CancellationToken.None);

        result.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetInteractiveLoginDenialAsync_TwoFactorRequired_FailsClosed()
    {
        var user = ExistingUser();
        _settings.Setup(s => s.IsTwoFactorRequiredForUser(user, It.IsAny<SystemAuthSettings>(), It.IsAny<TenantAuthSettings>())).Returns(true);

        var denial = await CreateService().GetInteractiveLoginDenialAsync(user.Id, _tenant.Id, CancellationToken.None);

        denial.Should().NotBeNull();
    }

    // ---- Tenant from URL / platform scope ----

    [Fact]
    public async Task RefreshTokenAsync_OnAnotherTenantsHost_IsRejected()
    {
        var user = ExistingUser();
        const string rawRefresh = "tenant-a-refresh-token";
        var session = RefreshToken.Create(user.Id, _tenant.Id, OpaqueTokenHasher.Hash(rawRefresh), DateTime.UtcNow.AddDays(1));
        _refreshTokens.Setup(r => r.GetByTokenAsync(OpaqueTokenHasher.Hash(rawRefresh), It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var act = () => CreateService().RefreshTokenAsync(
            new RefreshTokenDto.RefreshTokenRequest(rawRefresh, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        session.IsTokenValid.Should().BeTrue("a rejected cross-tenant refresh must not consume the session");
    }

    [Fact]
    public async Task SignupTenantAsync_NewAdminRole_GetsNoPlatformPermissions()
    {
        var permissions = new Mock<IPermissionRepository>();
        var catalog = new[]
        {
            new Permission { Id = Guid.NewGuid(), PermissionCode = ERP.Shared.Authorization.PermissionConstants.Identity_UsersManage, Module = "Identity" },
            new Permission { Id = Guid.NewGuid(), PermissionCode = ERP.Shared.Authorization.PermissionConstants.Identity_TenantsManage, Module = "Identity" },
            new Permission { Id = Guid.NewGuid(), PermissionCode = ERP.Shared.Authorization.PermissionConstants.Settings_SystemManage, Module = "Identity" },
            new Permission { Id = Guid.NewGuid(), PermissionCode = ERP.Shared.Authorization.PermissionConstants.Identity_PermissionsManage, Module = "Identity" },
        };
        permissions.Setup(p => p.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalog);
        permissions.Setup(p => p.GetPermissionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
        _uow.Setup(u => u.Permissions).Returns(permissions.Object);

        await CreateService().SignupTenantAsync(
            new TenantSignupDto.TenantSignupRequest("NEWCO", "New Co", "owner@newco.test", "Str0ng-Passw0rd!"), CancellationToken.None);

        var granted = permissions.Invocations
            .Where(i => i.Method.Name == nameof(IPermissionRepository.AssignToRoleAsync))
            .Select(i => (Guid)i.Arguments[1])
            .ToList();
        granted.Should().ContainSingle().Which.Should().Be(catalog[0].Id);
    }

    // ---- P0-7: external login (Google / Microsoft / Facebook) ----

    private static ExternalLoginDto.ExternalLoginRequest GoogleRequest() => new("Google", "g-1", "id-token", UserType.Admin);

    private void GoogleReturns(string email, bool emailVerified, string? fullName = "New Person") =>
        _googleProvider.Setup(p => p.VerifyAsync(It.IsAny<ExternalLoginDto.ExternalLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLoginDto.ExternalLoginResponse("Google", "g-1", email, emailVerified, fullName));

    [Fact]
    public async Task ExternalLoginAsync_FirstLogin_ProvisionsACustomer_WithConfirmedEmailAndATenantScopedLink()
    {
        GoogleReturns("New.Person@Acme.test", emailVerified: true);
        User? created = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => created = u);

        var response = await CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        response.Succeeded.Should().BeTrue();
        response.AccessToken.Should().Be("access-token");
        created.Should().NotBeNull();
        created!.UserTypeCode.Should().Be(nameof(UserType.Customer), "the client-supplied UserType is ignored");
        created.Email.Should().Be("new.person@acme.test");
        created.FullName.Should().Be("New Person");
        created.IsEmailConfirmed.Should().BeTrue("Google vouched for the address");
        created.PasswordHash.Should().NotBeNullOrWhiteSpace("User.Create refuses an empty hash; the account gets a random one nobody knows");
        var link = created.ExternalLogins.Should().ContainSingle().Which;
        link.Provider.Should().Be("Google");
        link.ProviderKey.Should().Be("g-1");
        link.TenantId.Should().Be(_tenant.Id, "lookups filter by tenant, so a link without it could never be found again");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExternalLoginAsync_SecondLogin_UsesTheLinkedUser_AndCreatesNothing()
    {
        GoogleReturns("new.person@acme.test", emailVerified: true);
        var linked = User.Create("new.person@acme.test", "new.person@acme.test", "hashed:x", _tenant.Id, UserType.Customer);
        _users.Setup(u => u.GetByExternalProviderAsync("Google", "g-1", _tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(linked);

        var response = await CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        response.Succeeded.Should().BeTrue();
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLogins.Verify(l => l.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExternalLoginAsync_VerifiedEmailOfAnExistingAccount_LinksIt_InsteadOfCreatingADuplicate()
    {
        var existing = ExistingUser(); // victim@acme.test, signs in with a password today
        GoogleReturns(existing.Email, emailVerified: true);
        ExternalLogin? link = null;
        _externalLogins.Setup(l => l.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalLogin, CancellationToken>((l, _) => link = l);

        var response = await CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        response.Succeeded.Should().BeTrue();
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        link.Should().NotBeNull();
        link!.UserId.Should().Be(existing.Id);
        link.TenantId.Should().Be(_tenant.Id);
        existing.IsEmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ExternalLoginAsync_UnverifiedEmailOfAnExistingAccount_IsRefused_NotTakenOver()
    {
        var existing = ExistingUser();
        GoogleReturns(existing.Email, emailVerified: false);

        var act = () => CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _externalLogins.Verify(l => l.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExternalLoginAsync_UnverifiedEmailOfANewPerson_StillCreatesACustomer_WithAnUnconfirmedEmail()
    {
        GoogleReturns("fresh@acme.test", emailVerified: false);
        User? created = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => created = u);

        var response = await CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        response.Succeeded.Should().BeTrue();
        created!.IsEmailConfirmed.Should().NotBe(true);
        created.UserTypeCode.Should().Be(nameof(UserType.Customer));
    }

    [Fact]
    public async Task ExternalLoginAsync_AccountWithoutAnEmail_IsRefused()
    {
        GoogleReturns("", emailVerified: false);

        var act = () => CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ---- P0-7: the 2FA code must be sent to the real address ----

    private static void RequireTwoFactor(Mock<ISettingsService> settings, User user) =>
        settings.Setup(s => s.IsTwoFactorRequiredForUser(user, It.IsAny<SystemAuthSettings>(), It.IsAny<TenantAuthSettings>())).Returns(true);

    [Fact]
    public async Task TwoFactor_ByEmail_TheEventCarriesTheRealAddress_WhileTheChallengeKeepsAMaskedOne()
    {
        var user = ExistingUser();
        user.EnableTwoFactor("Email");
        GoogleReturns(user.Email, emailVerified: true);
        _users.Setup(u => u.GetByExternalProviderAsync("Google", "g-1", _tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        RequireTwoFactor(_settings, user);
        TwoFactorChallenge? challenge = null;
        _twoFactorChallenges.Setup(c => c.AddAsync(It.IsAny<TwoFactorChallenge>(), It.IsAny<CancellationToken>()))
            .Callback<TwoFactorChallenge, CancellationToken>((c, _) => challenge = c);

        var response = await CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        response.RequiresTwoFactor.Should().BeTrue();
        challenge.Should().NotBeNull();
        challenge!.Destination.Should().Contain("*").And.NotBe(user.Email, "what is stored / shown is masked");
        var evt = challenge.DomainEvents.OfType<TwoFactorCodeGeneratedDomainEvent>().Single();
        evt.Destination.Should().Be(user.Email, "NotificationService delivers to this value");
        evt.Method.Should().Be("Email");
    }

    [Fact]
    public async Task TwoFactor_BySms_TheEventCarriesTheRealPhoneNumber()
    {
        var user = ExistingUser();
        user.EnableTwoFactor("Sms", "+84901234567");
        GoogleReturns(user.Email, emailVerified: true);
        _users.Setup(u => u.GetByExternalProviderAsync("Google", "g-1", _tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        RequireTwoFactor(_settings, user);
        TwoFactorChallenge? challenge = null;
        _twoFactorChallenges.Setup(c => c.AddAsync(It.IsAny<TwoFactorChallenge>(), It.IsAny<CancellationToken>()))
            .Callback<TwoFactorChallenge, CancellationToken>((c, _) => challenge = c);

        await CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        challenge!.Destination.Should().EndWith("4567").And.Contain("*");
        challenge.DomainEvents.OfType<TwoFactorCodeGeneratedDomainEvent>().Single().Destination.Should().Be("+84901234567");
    }

    [Fact]
    public async Task TwoFactor_BySmsWithoutAPhoneNumber_FailsInsteadOfCreatingACodeNobodyCanReceive()
    {
        var user = ExistingUser();
        user.EnableTwoFactor("Sms"); // no phone number on file
        GoogleReturns(user.Email, emailVerified: true);
        _users.Setup(u => u.GetByExternalProviderAsync("Google", "g-1", _tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        RequireTwoFactor(_settings, user);

        var act = () => CreateService().ExternalLoginAsync(GoogleRequest(), TenantCode, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>().Where(e => e.RuleName == "TwoFactorNoDestination");
        _twoFactorChallenges.Verify(c => c.AddAsync(It.IsAny<TwoFactorChallenge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}
