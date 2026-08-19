using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class WebhookEndpointRepository : GenericRepository<WebhookEndpoint>, IWebhookEndpointRepository
    {
        public WebhookEndpointRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<WebhookEndpoint>> ListByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<WebhookEndpoint>().AsNoTracking()
                .Where(w => w.TenantId == tenantId)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<WebhookEndpoint>> ListActiveByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<WebhookEndpoint>().AsNoTracking()
                .Where(w => w.TenantId == tenantId && w.IsActive)
                .ToListAsync(ct);
        }
    }
}
