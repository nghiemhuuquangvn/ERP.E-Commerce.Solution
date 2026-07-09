using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ILoginAttemptRepository : IGenericRepository<LoginAttempt>
    {
        Task<(IReadOnlyList<LoginAttempt> Items, int TotalCount)> SearchAsync(
            string? ipAddress, string? email, int page, int pageSize, CancellationToken ct);

        Task<int> CountRecentFailedAsync(string ipAddress, string? email, TimeSpan window, CancellationToken ct);
    }
}
