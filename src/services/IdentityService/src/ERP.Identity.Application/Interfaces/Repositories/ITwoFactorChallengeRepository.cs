using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ITwoFactorChallengeRepository : IGenericRepository<TwoFactorChallenge>
    {
        /// <summary>The user's challenges that could still be answered (not verified, not expired). Tracked, so a change
        /// made to them is saved by the unit of work.</summary>
        Task<IReadOnlyList<TwoFactorChallenge>> GetActiveForUserAsync(Guid userId, Guid tenantId, CancellationToken ct);
    }
}
