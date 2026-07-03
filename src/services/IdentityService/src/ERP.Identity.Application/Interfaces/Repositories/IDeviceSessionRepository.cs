using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IDeviceSessionRepository : IGenericRepository<DeviceSession>
    {
        Task<DeviceSession?> GetByUserAndDeviceAsync(Guid userId, string deviceId, CancellationToken ct);
        Task<IReadOnlyList<DeviceSession>> ListActiveByUserAsync(Guid userId, CancellationToken ct);
    }
}
