using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class TwoFactorChallengeRepository : GenericRepository<TwoFactorChallenge>, ITwoFactorChallengeRepository
    {
        public TwoFactorChallengeRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<TwoFactorChallenge>> GetActiveForUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            return await _dbContext.TwoFactorChallenges
                .Where(c => c.UserId == userId && c.TenantId == tenantId && c.VerifiedAt == null && c.ExpiredAt > now)
                .ToListAsync(ct);
        }

        public Task<int> CountCreatedSinceAsync(Guid userId, DateTime since, CancellationToken ct) =>
            _dbContext.TwoFactorChallenges.AsNoTracking().CountAsync(c => c.UserId == userId && c.CreatedAt >= since, ct);

        // SEC-M2: one atomic UPDATE per attempt - the row decides, not a value read earlier by this request.
        public async Task<bool> TryConsumeAttemptAsync(Guid challengeId, DateTime now, CancellationToken ct) =>
            await _dbContext.TwoFactorChallenges
                .Where(c => c.Id == challengeId && c.VerifiedAt == null && c.ExpiredAt > now && c.AttemptCount < c.MaxAttempts)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.AttemptCount, c => c.AttemptCount + 1), ct) == 1;

        public async Task<bool> TryMarkVerifiedAsync(Guid challengeId, DateTime now, CancellationToken ct) =>
            await _dbContext.TwoFactorChallenges
                .Where(c => c.Id == challengeId && c.VerifiedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.VerifiedAt, (DateTime?)now), ct) == 1;
    }
}
