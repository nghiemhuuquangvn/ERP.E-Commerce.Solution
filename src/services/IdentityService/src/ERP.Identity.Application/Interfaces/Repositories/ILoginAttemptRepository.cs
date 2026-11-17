using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ILoginAttemptRepository : IGenericRepository<LoginAttempt>
    {
        Task<(IReadOnlyList<LoginAttempt> Items, int TotalCount)> SearchAsync(
            string? ipAddress, string? email, int page, int pageSize, CancellationToken ct);

        Task<int> CountRecentFailedAsync(string ipAddress, string? email, TimeSpan window, CancellationToken ct);

        /// <summary>Failures (password or 2FA) of one account from one IP since its last sign-in from there (BR-BF-01).</summary>
        Task<int> CountRecentFailedForUserFromIpAsync(Guid userId, string ipAddress, TimeSpan window, CancellationToken ct);

        /// <summary>Failures (password or 2FA) of one account from every IP since its last sign-in (BR-BF-02).</summary>
        Task<int> CountRecentFailedForUserAsync(Guid userId, TimeSpan window, CancellationToken ct);
    }
}
