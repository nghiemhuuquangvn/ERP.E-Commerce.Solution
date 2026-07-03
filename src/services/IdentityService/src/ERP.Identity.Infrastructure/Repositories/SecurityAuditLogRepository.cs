using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class SecurityAuditLogRepository : GenericRepository<SecurityAuditLog>, ISecurityAuditLogRepository
    {
        public SecurityAuditLogRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<(IReadOnlyList<SecurityAuditLog> Items, int TotalCount)> SearchAsync(
            Guid? tenantId, Guid? userId, string? eventType, int page, int pageSize, CancellationToken ct)
        {
            var query = _dbContext.Set<SecurityAuditLog>().AsNoTracking().AsQueryable();

            if (tenantId.HasValue)
                query = query.Where(l => l.TenantId == tenantId);
            if (userId.HasValue)
                query = query.Where(l => l.UserId == userId);
            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(l => l.EventType == eventType);

            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(l => l.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }
    }
}
