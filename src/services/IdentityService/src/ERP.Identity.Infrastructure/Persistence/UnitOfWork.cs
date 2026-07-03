using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
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
        private IDbContextTransaction? _currentTrans;

        public UnitOfWork(AuthDbContext dbContext,
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IPermissionRepository permissionRepository,
            IRefreshTokenRepository refreshTokenRepository,
            ITenantRepository tenantRepository,
            IExternalLoginRepository externalLoginRepository,
            IOutboxMessageRepository outboxMessageRepository,
            IPasswordResetTokenRepository passwordResetTokenRepository,
            ISystemAuthSettingsRepository systemAuthSettingsRepository,
            ITenantAuthSettingsRepository tenantAuthSettingsRepository,
            ITwoFactorChallengeRepository twoFactorChallengeRepository,
            IOAuthStateRepository oAuthStateRepository,
            ISecurityAuditLogRepository securityAuditLogRepository,
            ILoginAttemptRepository loginAttemptRepository,
            IDeviceSessionRepository deviceSessionRepository,
            ITokenBlacklistRepository tokenBlacklistRepository,
            IPasswordHistoryRepository passwordHistoryRepository,
            IApiKeyRepository apiKeyRepository)
        {
            _dbContext = dbContext;
            Users = userRepository;
            Roles = roleRepository;
            Permissions = permissionRepository;
            RefreshTokens = refreshTokenRepository;
            Tenants = tenantRepository;
            ExternalLogins = externalLoginRepository;
            OutboxMessages = outboxMessageRepository;
            PasswordResetTokens = passwordResetTokenRepository;
            SystemAuthSettings = systemAuthSettingsRepository;
            TenantAuthSettings = tenantAuthSettingsRepository;
            TwoFactorChallenges = twoFactorChallengeRepository;
            OAuthStates = oAuthStateRepository;
            SecurityAuditLogs = securityAuditLogRepository;
            LoginAttempts = loginAttemptRepository;
            DeviceSessions = deviceSessionRepository;
            TokenBlacklist = tokenBlacklistRepository;
            PasswordHistories = passwordHistoryRepository;
            ApiKeys = apiKeyRepository;
        }
        public IUserRepository Users { get; }

        public IRoleRepository Roles { get; }

        public IPermissionRepository Permissions { get; }

        public IRefreshTokenRepository RefreshTokens { get; }
        public ITenantRepository Tenants { get; }
        public IExternalLoginRepository ExternalLogins { get; }
        public IOutboxMessageRepository OutboxMessages { get; }
        public IPasswordResetTokenRepository PasswordResetTokens { get; }
        public ISystemAuthSettingsRepository SystemAuthSettings { get; }
        public ITenantAuthSettingsRepository TenantAuthSettings { get; }
        public ITwoFactorChallengeRepository TwoFactorChallenges { get; }
        public IOAuthStateRepository OAuthStates { get; }
        public ISecurityAuditLogRepository SecurityAuditLogs { get; }
        public ILoginAttemptRepository LoginAttempts { get; }
        public IDeviceSessionRepository DeviceSessions { get; }
        public ITokenBlacklistRepository TokenBlacklist { get; }
        public IPasswordHistoryRepository PasswordHistories { get; }
        public IApiKeyRepository ApiKeys { get; }

        public async Task<int> SaveChangesAsync(CancellationToken ct) => await _dbContext.SaveChangesAsync(ct);
        public async Task BeginTransactionAsync(CancellationToken ct)
        {
            _currentTrans = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitTransactionAsync(CancellationToken ct = default)
        {
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                if (_currentTrans != null) await _currentTrans.CommitAsync(ct);
            }
            catch
            {
                await RollbackTransactionAsync(ct);
                throw;
            }
            finally
            {
                if (_currentTrans != null)
                {
                    _currentTrans.Dispose();
                    _currentTrans = null;
                }
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken ct)
        {
            if (_currentTrans != null)
            {
                await _currentTrans.RollbackAsync(ct);
                _currentTrans.Dispose();
                _currentTrans = null;
            }
        }

        public void Attach<TEntity>(TEntity entity) where TEntity : class
        {
            _dbContext.Attach(entity);
        }

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
                _currentTrans?.Dispose();
            }
        }

        
    }
}
