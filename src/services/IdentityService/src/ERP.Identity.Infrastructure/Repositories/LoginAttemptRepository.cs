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

        public async Task<int> CountRecentFailedAsync(string ipAddress, string? email, TimeSpan window, CancellationToken ct)
        {
            var since = DateTime.UtcNow - window;
            var query = _dbContext.Set<LoginAttempt>().AsNoTracking()
                .Where(l => l.IpAddress == ipAddress && !l.IsSuccessful && l.AttemptedAt >= since);

            // Scoped to (ip, email) when an email is known, not IP alone — otherwise a
            // shared-NAT/corporate IP would get locked out by one account's failures.
            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(l => l.Email == email);

            return await query.CountAsync(ct);
        }
    }
}
