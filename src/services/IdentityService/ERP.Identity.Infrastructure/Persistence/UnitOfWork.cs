using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly AuthDbContext _dbContext;

        public UnitOfWork(AuthDbContext dbContext,
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IPermissionRepository permissionRepository,
            IRefreshTokenRepository refreshTokenRepository,
            ITenantRepository tenantRepository)
        {
            _dbContext = dbContext;
            Users = userRepository;
            Roles = roleRepository;
            Permissions = permissionRepository;
            RefreshTokens = refreshTokenRepository;
            Tenants = tenantRepository;
        }
        public IUserRepository Users { get; }

        public IRoleRepository Roles { get; }

        public IPermissionRepository Permissions { get; }

        public IRefreshTokenRepository RefreshTokens { get; }
        public ITenantRepository Tenants { get; }

        public async Task<int> SaveChangesAsync(CancellationToken ct) => await _dbContext.SaveChangesAsync(ct);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dbContext.Dispose();
            }
        }
    }
}
