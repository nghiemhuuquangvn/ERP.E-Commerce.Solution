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
        IExternalLoginRepository ExternalLogins { get; }
        IOutboxMessageRepository OutboxMessages { get; }
        IPasswordResetTokenRepository PasswordResetTokens { get; }
        Task<int> SaveChangesAsync(CancellationToken ct);
        Task BeginTransactionAsync(CancellationToken ct);
        Task CommitTransactionAsync(CancellationToken ct);
        Task RollbackTransactionAsync(CancellationToken ct);
         void Dispose();
    }
}
