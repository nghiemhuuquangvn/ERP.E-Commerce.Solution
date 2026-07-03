using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class DeviceSessionRepository : GenericRepository<DeviceSession>, IDeviceSessionRepository
    {
        public DeviceSessionRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<DeviceSession?> GetByUserAndDeviceAsync(Guid userId, string deviceId, CancellationToken ct)
        {
            return await _dbContext.Set<DeviceSession>()
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId, ct);
        }

        public async Task<IReadOnlyList<DeviceSession>> ListActiveByUserAsync(Guid userId, CancellationToken ct)
        {
            return await _dbContext.Set<DeviceSession>().AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive)
                .OrderByDescending(d => d.LastActiveAt)
                .ToListAsync(ct);
        }
    }
}
