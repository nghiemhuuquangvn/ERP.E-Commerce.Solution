using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface ISystemAuthSettingsRepository : IGenericRepository<SystemAuthSettings>
    {
        Task<SystemAuthSettings?> GetAsync(CancellationToken ct);
    }
}
