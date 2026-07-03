using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class LoginAttemptRepository : GenericRepository<LoginAttempt>, ILoginAttemptRepository
    {
        public LoginAttemptRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<(IReadOnlyList<LoginAttempt> Items, int TotalCount)> SearchAsync(
            string? ipAddress, string? email, int page, int pageSize, CancellationToken ct)
        {
            var query = _dbContext.Set<LoginAttempt>().AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(ipAddress))
                query = query.Where(l => l.IpAddress == ipAddress);
            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(l => l.Email == email);

            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(l => l.AttemptedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public async Task<int> CountRecentFailedAsync(string ipAddress, TimeSpan window, CancellationToken ct)
        {
            var since = DateTime.UtcNow - window;
            return await _dbContext.Set<LoginAttempt>().AsNoTracking()
                .CountAsync(l => l.IpAddress == ipAddress && !l.IsSuccessful && l.AttemptedAt >= since, ct);
        }
    }
}
