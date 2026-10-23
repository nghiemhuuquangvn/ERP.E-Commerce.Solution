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
    }
}
