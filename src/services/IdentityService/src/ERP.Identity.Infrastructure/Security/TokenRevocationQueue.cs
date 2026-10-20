using ERP.Identity.Application.Interfaces.Services;
using ERP.Shared.Authorization.TokenRevocation;
using Microsoft.Extensions.Logging;

namespace ERP.Identity.Infrastructure.Security
{
    /// <inheritdoc cref="ITokenRevocationQueue"/>
    public sealed class TokenRevocationQueue : ITokenRevocationQueue
    {
        private readonly ITokenRevocationStore _store;
        private readonly ILogger<TokenRevocationQueue> _logger;
        private readonly List<(string Jti, DateTime ExpiresAt)> _tokens = [];
        private readonly HashSet<Guid> _users = [];
        private readonly HashSet<Guid> _tenants = [];

        public TokenRevocationQueue(ITokenRevocationStore store, ILogger<TokenRevocationQueue> logger)
        {
            _store = store;
            _logger = logger;
        }

        public void RevokeToken(string jti, DateTime expiresAtUtc)
        {
            if (!string.IsNullOrWhiteSpace(jti))
                _tokens.Add((jti, expiresAtUtc));
        }

        public void UsersChanged(params Guid[] userIds)
        {
            foreach (var id in userIds.Where(id => id != Guid.Empty))
                _users.Add(id);
        }

        public void TenantChanged(Guid tenantId)
        {
            if (tenantId != Guid.Empty)
                _tenants.Add(tenantId);
        }

        public async Task FlushAsync(CancellationToken ct)
        {
            if (_tokens.Count == 0 && _users.Count == 0 && _tenants.Count == 0)
                return;

            var tokens = _tokens.ToList();
            var users = _users.ToList();
            var tenants = _tenants.ToList();
            Clear();

            try
            {
                foreach (var (jti, expiresAt) in tokens)
                    await _store.RevokeTokenAsync(jti, expiresAt, ct);
                if (users.Count > 0)
                    await _store.MarkUsersChangedAsync(users, ct);
                foreach (var tenant in tenants)
                    await _store.MarkTenantChangedAsync(tenant, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The change is committed; other services keep accepting the old tokens until
                // they expire. Loud on purpose — this is a security-relevant failure.
                _logger.LogError(ex,
                    "Publishing token revocations failed ({Tokens} tokens, {Users} users, {Tenants} tenants) — " +
                    "other services will accept the affected tokens until they expire",
                    tokens.Count, users.Count, tenants.Count);
            }
        }

        public void Clear()
        {
            _tokens.Clear();
            _users.Clear();
            _tenants.Clear();
        }
    }
}
