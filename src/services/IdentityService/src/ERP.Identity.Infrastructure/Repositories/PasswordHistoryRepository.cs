using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class PasswordHistoryRepository : GenericRepository<PasswordHistoryEntry>, IPasswordHistoryRepository
    {
        public PasswordHistoryRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<PasswordHistoryEntry>> GetRecentAsync(Guid userId, int count, CancellationToken ct)
        {
            return await _dbContext.Set<PasswordHistoryEntry>().AsNoTracking()
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync(ct);
        }
    }
}
