using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ERP.Shared.Authorization.TokenRevocation;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.Shared.UnitTests.Authorization;

public class TokenRevocationTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid OtherUser = Guid.NewGuid();
    private static readonly Guid Tenant = Guid.NewGuid();

    private static ClaimsPrincipal Token(long? iat, string? jti = null, Guid? user = null) =>
        new(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, (user ?? User).ToString()),
                new Claim("tenant_id", Tenant.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, jti ?? Guid.NewGuid().ToString()),
            }
            .Concat(iat is { } v ? new[] { new Claim(JwtRegisteredClaimNames.Iat, v.ToString()) } : []), "Bearer"));

    private static TokenRevocationValidator Validator(ITokenRevocationStore store, int localCacheSeconds = 0) =>
        new(store, new MemoryCache(new MemoryCacheOptions()), NullLogger<TokenRevocationValidator>.Instance,
            new TokenRevocationOptions { LocalCacheSeconds = localCacheSeconds });

    [Fact]
    public async Task UntouchedToken_IsAccepted()
    {
        (await Validator(new InMemoryTokenRevocationStore()).IsRevokedAsync(Token(1000))).Should().BeFalse();
    }

    [Fact]
    public async Task LoggedOutJti_IsRejected_OtherTokensOfTheUserAreNot()
    {
        var store = new InMemoryTokenRevocationStore();
        await store.RevokeTokenAsync("jti-1", DateTime.UtcNow.AddMinutes(30));

        var validator = Validator(store);
        (await validator.IsRevokedAsync(Token(1000, "jti-1"))).Should().BeTrue();
        (await validator.IsRevokedAsync(Token(1000, "jti-2"))).Should().BeFalse();
    }

    [Fact]
    public async Task TokenIssuedBeforeUserChange_IsRejected_NewerTokenAndOtherUsersAreNot()
    {
        var store = new InMemoryTokenRevocationStore { Clock = () => 2000 };
        await store.MarkUsersChangedAsync([User]);

        var validator = Validator(store);
        (await validator.IsRevokedAsync(Token(1999))).Should().BeTrue("issued before the change");
        (await validator.IsRevokedAsync(Token(2000))).Should().BeFalse("issued at/after the change (refreshed)");
        (await validator.IsRevokedAsync(Token(1999, user: OtherUser))).Should().BeFalse("another user is unaffected");
    }

    [Fact]
    public async Task TokenIssuedBeforeTenantChange_IsRejected()
    {
        var store = new InMemoryTokenRevocationStore { Clock = () => 5000 };
        await store.MarkTenantChangedAsync(Tenant);

        (await Validator(store).IsRevokedAsync(Token(4999, user: OtherUser))).Should().BeTrue();
    }

    [Fact]
    public async Task LegacyTokenWithoutIat_IsOnlyCheckedByJti()
    {
        var store = new InMemoryTokenRevocationStore { Clock = () => 2000 };
        await store.MarkUsersChangedAsync([User]);
        await store.RevokeTokenAsync("old", DateTime.UtcNow.AddMinutes(5));

        var validator = Validator(store);
        (await validator.IsRevokedAsync(Token(null, "legacy"))).Should().BeFalse();
        (await validator.IsRevokedAsync(Token(null, "old"))).Should().BeTrue();
    }

    [Fact]
    public async Task StoreUnreachable_FailsOpen()
    {
        var store = new Mock<ITokenRevocationStore>();
        store.Setup(s => s.GetStateAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("redis down"));

        (await Validator(store.Object).IsRevokedAsync(Token(1000))).Should().BeFalse();
    }

    [Fact]
    public async Task LocalCache_AvoidsAStoreCallPerRequest()
    {
        var store = new Mock<ITokenRevocationStore>();
        store.Setup(s => s.GetStateAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RevocationState.None);
        var validator = Validator(store.Object, localCacheSeconds: 5);
        var token = Token(1000, "same");

        await validator.IsRevokedAsync(token);
        await validator.IsRevokedAsync(token);

        store.Verify(s => s.GetStateAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpiredJtiRevocation_IsNotStored()
    {
        var store = new InMemoryTokenRevocationStore();
        await store.RevokeTokenAsync("expired", DateTime.UtcNow.AddSeconds(-1));

        (await store.GetStateAsync("expired", null, null)).JtiRevoked.Should().BeFalse();
    }
}
