using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IUnitOfWork
    {
        IUserRepository Users { get; }
        IRoleRepository Roles { get; }
        IPermissionRepository Permissions { get; }
        IRefreshTokenRepository RefreshTokens { get; }
        ITenantRepository Tenants { get; }
        Task<int> SaveChangesAsync(CancellationToken ct);
         void Dispose();
    }
}
