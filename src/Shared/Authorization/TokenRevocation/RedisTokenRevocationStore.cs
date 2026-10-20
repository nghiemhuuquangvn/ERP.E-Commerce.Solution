using StackExchange.Redis;

namespace ERP.Shared.Authorization.TokenRevocation;

/// <summary>Redis-backed <see cref="ITokenRevocationStore"/> (one MGET per check).</summary>
public sealed class RedisTokenRevocationStore : ITokenRevocationStore
{
    private readonly Lazy<Task<IConnectionMultiplexer>> _connection;
    private readonly TimeSpan _markerTtl;

    public RedisTokenRevocationStore(string connectionString, TokenRevocationOptions options)
    {
        var config = ConfigurationOptions.Parse(connectionString);
        // Never block startup or crash when Redis is down: the validator fails open (decision
        // 2026-10-20) and the multiplexer keeps reconnecting in the background.
        config.AbortOnConnectFail = false;
        config.ConnectTimeout = 2000;
        config.SyncTimeout = 1000;
        config.AsyncTimeout = 1000;
        _connection = new Lazy<Task<IConnectionMultiplexer>>(
            async () => await ConnectionMultiplexer.ConnectAsync(config));
        _markerTtl = TimeSpan.FromMinutes(options.ChangeMarkerTtlMinutes);
    }

    private async Task<IDatabase> DbAsync() => (await _connection.Value).GetDatabase();

    public async Task<RevocationState> GetStateAsync(string? jti, string? userId, string? tenantId, CancellationToken ct = default)
    {
        var keys = new RedisKey[]
        {
            string.IsNullOrEmpty(jti) ? TokenRevocationKeys.Prefix + "none" : TokenRevocationKeys.RevokedJti(jti),
            string.IsNullOrEmpty(userId) ? TokenRevocationKeys.Prefix + "none" : TokenRevocationKeys.UserChangedAt(userId),
            string.IsNullOrEmpty(tenantId) ? TokenRevocationKeys.Prefix + "none" : TokenRevocationKeys.TenantChangedAt(tenantId),
        };
        var values = await (await DbAsync()).StringGetAsync(keys);
        return new RevocationState(
            !string.IsNullOrEmpty(jti) && values[0].HasValue,
            values[1].HasValue && long.TryParse(values[1].ToString(), out var user) ? user : null,
            values[2].HasValue && long.TryParse(values[2].ToString(), out var tenant) ? tenant : null);
    }

    public async Task RevokeTokenAsync(string jti, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        var ttl = expiresAtUtc.ToUniversalTime() - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
            return; // already expired — nothing to revoke
        await (await DbAsync()).StringSetAsync(TokenRevocationKeys.RevokedJti(jti), "1", ttl);
    }

    public async Task MarkUsersChangedAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var db = await DbAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        await Task.WhenAll(userIds.Distinct().Select(id =>
            db.StringSetAsync(TokenRevocationKeys.UserChangedAt(id.ToString()), now, _markerTtl)));
    }

    public async Task MarkTenantChangedAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        await (await DbAsync()).StringSetAsync(TokenRevocationKeys.TenantChangedAt(tenantId.ToString()), now, _markerTtl);
    }
}

/// <summary>
/// Process-local store: unit tests, and environments without <c>ConnectionStrings:Redis</c> —
/// there revocation only works inside the process that recorded it (logged at startup).
/// </summary>
public sealed class InMemoryTokenRevocationStore : ITokenRevocationStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Value, DateTime ExpiresAt)> _entries = new();
    private readonly TimeSpan _markerTtl;

    public InMemoryTokenRevocationStore(TokenRevocationOptions? options = null)
    {
        _markerTtl = TimeSpan.FromMinutes((options ?? new TokenRevocationOptions()).ChangeMarkerTtlMinutes);
    }

    /// <summary>Test hook: overrides "now" (unix seconds) for change markers.</summary>
    public Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private string? Get(string key) =>
        _entries.TryGetValue(key, out var e) && e.ExpiresAt > DateTime.UtcNow ? e.Value : null;

    public Task<RevocationState> GetStateAsync(string? jti, string? userId, string? tenantId, CancellationToken ct = default)
    {
        var user = string.IsNullOrEmpty(userId) ? null : Get(TokenRevocationKeys.UserChangedAt(userId));
        var tenant = string.IsNullOrEmpty(tenantId) ? null : Get(TokenRevocationKeys.TenantChangedAt(tenantId));
        return Task.FromResult(new RevocationState(
            !string.IsNullOrEmpty(jti) && Get(TokenRevocationKeys.RevokedJti(jti)) != null,
            long.TryParse(user, out var u) ? u : null,
            long.TryParse(tenant, out var t) ? t : null));
    }

    public Task RevokeTokenAsync(string jti, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        if (expiresAtUtc.ToUniversalTime() > DateTime.UtcNow)
            _entries[TokenRevocationKeys.RevokedJti(jti)] = ("1", expiresAtUtc.ToUniversalTime());
        return Task.CompletedTask;
    }

    public Task MarkUsersChangedAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        foreach (var id in userIds.Distinct())
            _entries[TokenRevocationKeys.UserChangedAt(id.ToString())] = (Clock().ToString(), DateTime.UtcNow + _markerTtl);
        return Task.CompletedTask;
    }

    public Task MarkTenantChangedAsync(Guid tenantId, CancellationToken ct = default)
    {
        _entries[TokenRevocationKeys.TenantChangedAt(tenantId.ToString())] = (Clock().ToString(), DateTime.UtcNow + _markerTtl);
        return Task.CompletedTask;
    }
}
