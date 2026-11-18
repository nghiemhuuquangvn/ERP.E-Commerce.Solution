using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
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

        public IReadOnlyList<RevocationOutboxMessage> Stage()
        {
            if (_tokens.Count == 0 && _users.Count == 0 && _tenants.Count == 0)
                return [];

            var now = DateTime.UtcNow;
            var rows = new List<RevocationOutboxMessage>();
            rows.AddRange(_tokens.Select(t => RevocationOutboxMessage.ForToken(t.Jti, t.ExpiresAt, now)));
            rows.AddRange(_users.Select(u => RevocationOutboxMessage.ForUser(u, now)));
            rows.AddRange(_tenants.Select(t => RevocationOutboxMessage.ForTenant(t, now)));
            Clear();
            return rows;
        }

        public async Task PublishAsync(IReadOnlyList<RevocationOutboxMessage> rows, CancellationToken ct)
        {
            foreach (var row in rows.Where(r => !r.IsPublished))
            {
                try
                {
                    await PublishOneAsync(_store, row, ct);
                    row.MarkPublished(DateTime.UtcNow);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Kept in the outbox: the RevocationOutboxPublisher retries until it reaches the store (S4a).
                    row.MarkFailed(ex.Message);
                    var level = row.Attempts >= 3 ? LogLevel.Error : LogLevel.Warning;
                    _logger.Log(level, ex,
                        "Publishing a token revocation ({Kind} {Subject}) failed (attempt {Attempts}); it stays in the outbox and is retried",
                        row.Kind, row.Subject, row.Attempts);
                }
            }
        }

        /// <summary>Writes one outbox row to the shared store; throws when the store is unreachable.</summary>
        public static Task PublishOneAsync(ITokenRevocationStore store, RevocationOutboxMessage row, CancellationToken ct) => row.Kind switch
        {
            RevocationKind.Token => store.RevokeTokenAsync(row.Subject, row.ExpiresAt ?? DateTime.UtcNow, ct),
            RevocationKind.Users => store.MarkUsersChangedAsync([Guid.Parse(row.Subject)], ct, row.ChangedAtUnix),
            RevocationKind.Tenant => store.MarkTenantChangedAsync(Guid.Parse(row.Subject), ct, row.ChangedAtUnix),
            _ => Task.CompletedTask,
        };

        public Task FlushAsync(CancellationToken ct) => PublishAsync(Stage(), ct);

        public void Clear()
        {
            _tokens.Clear();
            _users.Clear();
            _tenants.Clear();
        }
    }
}
