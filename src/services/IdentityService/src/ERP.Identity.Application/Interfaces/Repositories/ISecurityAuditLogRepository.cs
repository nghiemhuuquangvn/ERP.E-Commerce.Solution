using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ISecurityAuditLogRepository : IGenericRepository<SecurityAuditLog>
    {
        Task<(IReadOnlyList<SecurityAuditLog> Items, int TotalCount)> SearchAsync(
            Guid? tenantId, Guid? userId, string? eventType, int page, int pageSize, CancellationToken ct);
    }
}
