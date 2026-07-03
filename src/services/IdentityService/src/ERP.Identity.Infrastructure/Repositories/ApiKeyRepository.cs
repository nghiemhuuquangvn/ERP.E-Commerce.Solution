using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class ApiKeyRepository : GenericRepository<ApiKey>, IApiKeyRepository
    {
        public ApiKeyRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<ApiKey?> GetByPrefixAsync(string keyPrefix, CancellationToken ct)
        {
            return await _dbContext.Set<ApiKey>()
                .FirstOrDefaultAsync(k => k.KeyPrefix == keyPrefix, ct);
        }

        public async Task<IReadOnlyList<ApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<ApiKey>().AsNoTracking()
                .Where(k => k.TenantId == tenantId)
                .OrderByDescending(k => k.CreatedAt)
                .ToListAsync(ct);
        }
    }
}
