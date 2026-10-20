using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Authorization.TokenRevocation;

/// <summary>
/// Decides whether a signature-valid access token has been revoked since it was issued:
/// its <c>jti</c> was revoked (logout), or it was issued (<c>iat</c>) before the last
/// security-relevant change of its user (password, lock, deactivation, roles/permissions)
/// or tenant (license, tenant status). Tokens without <c>iat</c> (issued before 2026-10-20)
/// are only checked by <c>jti</c> until they expire.
/// <para>
/// States are cached locally for <see cref="TokenRevocationOptions.LocalCacheSeconds"/> to
/// avoid a Redis round-trip per request. If the store is unreachable the token is ACCEPTED
/// (fail-open, decision 2026-10-20 — a Redis outage must not take every API down) and a
/// throttled warning is logged.
/// </para>
/// </summary>
public sealed class TokenRevocationValidator
{
    private static long _lastWarningTicks;

    private readonly ITokenRevocationStore _store;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenRevocationValidator> _logger;
    private readonly TimeSpan _localCache;

    public TokenRevocationValidator(ITokenRevocationStore store, IMemoryCache cache,
        ILogger<TokenRevocationValidator> logger, TokenRevocationOptions options)
    {
        _store = store;
        _cache = cache;
        _logger = logger;
        _localCache = TimeSpan.FromSeconds(Math.Max(0, options.LocalCacheSeconds));
    }

    public async Task<bool> IsRevokedAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var tenantId = principal.FindFirst("tenant_id")?.Value;
        long? issuedAt = long.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value, out var iat) ? iat : null;

        if (string.IsNullOrEmpty(jti) && string.IsNullOrEmpty(userId))
            return false; // nothing to check against

        var cacheKey = $"authz-state|{jti}|{userId}|{tenantId}";
        if (!_cache.TryGetValue(cacheKey, out RevocationState? state) || state is null)
        {
            try
            {
                state = await _store.GetStateAsync(jti, userId, tenantId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                WarnThrottled(ex);
                return false; // fail open
            }

            if (_localCache > TimeSpan.Zero)
                _cache.Set(cacheKey, state, _localCache);
        }

        return IsRevoked(state, issuedAt);
    }

    /// <summary>The rule itself (pure — unit-tested directly).</summary>
    public static bool IsRevoked(RevocationState state, long? issuedAt) =>
        state.JtiRevoked
        || (issuedAt is { } iat
            && ((state.UserChangedAt is { } user && iat < user)
                || (state.TenantChangedAt is { } tenant && iat < tenant)));

    private void WarnThrottled(Exception ex)
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastWarningTicks);
        if (now - last < TimeSpan.FromSeconds(30).Ticks || Interlocked.CompareExchange(ref _lastWarningTicks, now, last) != last)
            return;
        _logger.LogWarning(ex, "Token revocation store unreachable — accepting tokens without revocation check (fail-open)");
    }
}

/// <summary>
/// Re-checks revocation on EVERY hub method call (decision 2026-10-20): a SignalR connection
/// outlives the request that authenticated it, so a logged-out / locked / re-permissioned
/// user is cut off on the next invocation instead of when the connection drops.
/// </summary>
public sealed class RevocationHubFilter : IHubFilter
{
    private readonly TokenRevocationValidator _validator;

    public RevocationHubFilter(TokenRevocationValidator validator)
    {
        _validator = validator;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var context = invocationContext.Context;
        if (context.User?.Identity?.IsAuthenticated == true
            && await _validator.IsRevokedAsync(context.User, context.ConnectionAborted))
        {
            context.Abort();
            throw new HubException("Token has been revoked.");
        }

        return await next(invocationContext);
    }
}

public static class TokenRevocationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared revocation store (Redis when <c>ConnectionStrings:Redis</c> is set,
    /// otherwise process-local) and <see cref="TokenRevocationValidator"/>. Options from
    /// <c>Security:Revocation</c>. Called by AddErpJwtAuthentication; safe to call twice.
    /// </summary>
    public static IServiceCollection AddTokenRevocation(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new TokenRevocationOptions();
        configuration.GetSection("Security:Revocation").Bind(options);
        services.TryAddSingleton(options);
        services.AddMemoryCache();

        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
            services.TryAddSingleton<ITokenRevocationStore>(_ => new RedisTokenRevocationStore(redis, options));
        else
            services.TryAddSingleton<ITokenRevocationStore>(sp =>
            {
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<TokenRevocationValidator>().LogWarning(
                    "ConnectionStrings:Redis is not configured — token revocation only applies inside this process");
                return new InMemoryTokenRevocationStore(options);
            });

        services.TryAddSingleton<TokenRevocationValidator>();
        services.TryAddScoped<RevocationHubFilter>();
        return services;
    }
}
