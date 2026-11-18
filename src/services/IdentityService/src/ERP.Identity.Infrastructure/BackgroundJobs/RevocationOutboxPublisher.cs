using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Identity.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// S4a (SEC-M8, docs/business/Identity/TokenRevocation.md BR-REV-03): publishes the token revocations that are still
    /// in the outbox - the fast path right after a commit failed (Redis down, process stopped) - in the order they
    /// happened, until the shared store has them. A row is never dropped; publishing twice is harmless (a jti is set,
    /// a change marker is only ever raised).
    /// </summary>
    public sealed class RevocationOutboxPublisher : BackgroundService
    {
        public const int BatchSize = 100;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<RevocationOutboxPublisher> _logger;

        public RevocationOutboxPublisher(IServiceScopeFactory scopes, ILogger<RevocationOutboxPublisher> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishPendingAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The database itself was unreachable: try again next round.
                    _logger.LogWarning(ex, "Revocation outbox round failed; retrying in {Seconds} s", Interval.TotalSeconds);
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        /// <summary>One round: oldest unpublished rows first. Returns how many were published.</summary>
        public async Task<int> PublishPendingAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var queue = scope.ServiceProvider.GetRequiredService<ITokenRevocationQueue>();

            var rows = await db.RevocationOutbox
                .Where(r => r.PublishedAt == null)
                .OrderBy(r => r.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (rows.Count == 0)
                return 0;

            await queue.PublishAsync(rows, ct);
            await db.SaveChangesAsync(ct);
            return rows.Count(r => r.IsPublished);
        }
    }
}
