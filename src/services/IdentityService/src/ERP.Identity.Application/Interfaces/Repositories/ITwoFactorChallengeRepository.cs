using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ITwoFactorChallengeRepository : IGenericRepository<TwoFactorChallenge>
    {
        /// <summary>The user's challenges that could still be answered (not verified, not expired). Tracked, so a change
        /// made to them is saved by the unit of work.</summary>
        Task<IReadOnlyList<TwoFactorChallenge>> GetActiveForUserAsync(Guid userId, Guid tenantId, CancellationToken ct);

        /// <summary>How many codes were created for the user since <paramref name="since"/> (BR-BF-04: at most 5 per hour).</summary>
        Task<int> CountCreatedSinceAsync(Guid userId, DateTime since, CancellationToken ct);

        /// <summary>Atomically uses one attempt of a live challenge; false when none is left / expired / verified (SEC-M2).</summary>
        Task<bool> TryConsumeAttemptAsync(Guid challengeId, DateTime now, CancellationToken ct);

        /// <summary>Atomically marks the challenge verified; false when another request already did (one sign-in per challenge).</summary>
        Task<bool> TryMarkVerifiedAsync(Guid challengeId, DateTime now, CancellationToken ct);
    }
}
