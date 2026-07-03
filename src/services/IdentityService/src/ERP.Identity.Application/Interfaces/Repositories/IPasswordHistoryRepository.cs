using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IPasswordHistoryRepository : IGenericRepository<PasswordHistoryEntry>
    {
        Task<IReadOnlyList<PasswordHistoryEntry>> GetRecentAsync(Guid userId, int count, CancellationToken ct);
    }
}
