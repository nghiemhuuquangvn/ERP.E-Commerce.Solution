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
            IEmailConfirmTokenRepository emailConfirmTokenRepository,
            ISystemAuthSettingsRepository systemAuthSettingsRepository,
            ITenantAuthSettingsRepository tenantAuthSettingsRepository,
            ISystemGeneralSettingsRepository systemGeneralSettingsRepository,
            ITenantGeneralSettingsRepository tenantGeneralSettingsRepository,
            ITenantCompanySettingsRepository tenantCompanySettingsRepository,
            ISystemWorkflowSettingsRepository systemWorkflowSettingsRepository,
            ITenantWorkflowSettingsRepository tenantWorkflowSettingsRepository,
            ISystemFileStorageSettingsRepository systemFileStorageSettingsRepository,
            ITenantFileStorageSettingsRepository tenantFileStorageSettingsRepository,
            ISystemSchedulerSettingsRepository systemSchedulerSettingsRepository,
            ITenantSchedulerSettingsRepository tenantSchedulerSettingsRepository,
            ISystemIntegrationSettingsRepository systemIntegrationSettingsRepository,
            ITenantIntegrationSettingsRepository tenantIntegrationSettingsRepository,
            ISystemApiSettingsRepository systemApiSettingsRepository,
            ITenantApiSettingsRepository tenantApiSettingsRepository,
            ISystemAuditSettingsRepository systemAuditSettingsRepository,
            ITenantAuditSettingsRepository tenantAuditSettingsRepository,
            ISystemAdvancedSettingsRepository systemAdvancedSettingsRepository,
            ITenantAdvancedSettingsRepository tenantAdvancedSettingsRepository,
            ISystemLocalizationSettingsRepository systemLocalizationSettingsRepository,
            ITenantLocalizationSettingsRepository tenantLocalizationSettingsRepository,
            ISystemFeatureFlagRepository systemFeatureFlagRepository,
            ITenantFeatureFlagOverrideRepository tenantFeatureFlagOverrideRepository,
            IModuleDefinitionRepository moduleDefinitionRepository,
            ILicensePlanRepository licensePlanRepository,
            ILicensePlanModuleRepository licensePlanModuleRepository,
            ITenantLicenseRepository tenantLicenseRepository,
            ITenantLicenseModuleRepository tenantLicenseModuleRepository,
            ITwoFactorChallengeRepository twoFactorChallengeRepository,
            IOAuthStateRepository oAuthStateRepository,
            ISecurityAuditLogRepository securityAuditLogRepository,
            ILoginAttemptRepository loginAttemptRepository,
            IDeviceSessionRepository deviceSessionRepository,
            ITokenBlacklistRepository tokenBlacklistRepository,
            IPasswordHistoryRepository passwordHistoryRepository,
            IApiKeyRepository apiKeyRepository,
            IWebhookEndpointRepository webhookEndpointRepository,
            ERP.Identity.Application.Interfaces.Services.ITokenRevocationQueue revocations)
        {
            Revocations = revocations;
            _dbContext = dbContext;
            Users = userRepository;
            Roles = roleRepository;
            Permissions = permissionRepository;
            RefreshTokens = refreshTokenRepository;
            Tenants = tenantRepository;
            ExternalLogins = externalLoginRepository;
            OutboxMessages = outboxMessageRepository;
            PasswordResetTokens = passwordResetTokenRepository;
            EmailConfirmTokens = emailConfirmTokenRepository;
            SystemAuthSettings = systemAuthSettingsRepository;
            TenantAuthSettings = tenantAuthSettingsRepository;
            SystemGeneralSettings = systemGeneralSettingsRepository;
            TenantGeneralSettings = tenantGeneralSettingsRepository;
            TenantCompanySettings = tenantCompanySettingsRepository;
            SystemWorkflowSettings = systemWorkflowSettingsRepository;
            TenantWorkflowSettings = tenantWorkflowSettingsRepository;
            SystemFileStorageSettings = systemFileStorageSettingsRepository;
            TenantFileStorageSettings = tenantFileStorageSettingsRepository;
            SystemSchedulerSettings = systemSchedulerSettingsRepository;
            TenantSchedulerSettings = tenantSchedulerSettingsRepository;
            SystemIntegrationSettings = systemIntegrationSettingsRepository;
            TenantIntegrationSettings = tenantIntegrationSettingsRepository;
            SystemApiSettings = systemApiSettingsRepository;
            TenantApiSettings = tenantApiSettingsRepository;
            SystemAuditSettings = systemAuditSettingsRepository;
            TenantAuditSettings = tenantAuditSettingsRepository;
            SystemAdvancedSettings = systemAdvancedSettingsRepository;
            TenantAdvancedSettings = tenantAdvancedSettingsRepository;
            SystemLocalizationSettings = systemLocalizationSettingsRepository;
            TenantLocalizationSettings = tenantLocalizationSettingsRepository;
            SystemFeatureFlags = systemFeatureFlagRepository;
            TenantFeatureFlagOverrides = tenantFeatureFlagOverrideRepository;
            ModuleDefinitions = moduleDefinitionRepository;
            LicensePlans = licensePlanRepository;
            LicensePlanModules = licensePlanModuleRepository;
            TenantLicenses = tenantLicenseRepository;
            TenantLicenseModules = tenantLicenseModuleRepository;
            TwoFactorChallenges = twoFactorChallengeRepository;
            OAuthStates = oAuthStateRepository;
            SecurityAuditLogs = securityAuditLogRepository;
            LoginAttempts = loginAttemptRepository;
            DeviceSessions = deviceSessionRepository;
            TokenBlacklist = tokenBlacklistRepository;
            PasswordHistories = passwordHistoryRepository;
            ApiKeys = apiKeyRepository;
            WebhookEndpoints = webhookEndpointRepository;
        }
        public IUserRepository Users { get; }

        public IRoleRepository Roles { get; }

        public IPermissionRepository Permissions { get; }

        public IRefreshTokenRepository RefreshTokens { get; }
        public ITenantRepository Tenants { get; }
        public IExternalLoginRepository ExternalLogins { get; }
        public IOutboxMessageRepository OutboxMessages { get; }
        public IPasswordResetTokenRepository PasswordResetTokens { get; }
        public IEmailConfirmTokenRepository EmailConfirmTokens { get; }
        public ISystemAuthSettingsRepository SystemAuthSettings { get; }
        public ITenantAuthSettingsRepository TenantAuthSettings { get; }
        public ISystemGeneralSettingsRepository SystemGeneralSettings { get; }
        public ITenantGeneralSettingsRepository TenantGeneralSettings { get; }
        public ITenantCompanySettingsRepository TenantCompanySettings { get; }
        public ISystemWorkflowSettingsRepository SystemWorkflowSettings { get; }
        public ITenantWorkflowSettingsRepository TenantWorkflowSettings { get; }
        public ISystemFileStorageSettingsRepository SystemFileStorageSettings { get; }
        public ITenantFileStorageSettingsRepository TenantFileStorageSettings { get; }
        public ISystemSchedulerSettingsRepository SystemSchedulerSettings { get; }
        public ITenantSchedulerSettingsRepository TenantSchedulerSettings { get; }
        public ISystemIntegrationSettingsRepository SystemIntegrationSettings { get; }
        public ITenantIntegrationSettingsRepository TenantIntegrationSettings { get; }
        public ISystemApiSettingsRepository SystemApiSettings { get; }
        public ITenantApiSettingsRepository TenantApiSettings { get; }
        public ISystemAuditSettingsRepository SystemAuditSettings { get; }
        public ITenantAuditSettingsRepository TenantAuditSettings { get; }
        public ISystemAdvancedSettingsRepository SystemAdvancedSettings { get; }
        public ITenantAdvancedSettingsRepository TenantAdvancedSettings { get; }
        public ISystemLocalizationSettingsRepository SystemLocalizationSettings { get; }
        public ITenantLocalizationSettingsRepository TenantLocalizationSettings { get; }
        public ISystemFeatureFlagRepository SystemFeatureFlags { get; }
        public ITenantFeatureFlagOverrideRepository TenantFeatureFlagOverrides { get; }
        public IModuleDefinitionRepository ModuleDefinitions { get; }
        public ILicensePlanRepository LicensePlans { get; }
        public ILicensePlanModuleRepository LicensePlanModules { get; }
        public ITenantLicenseRepository TenantLicenses { get; }
        public ITenantLicenseModuleRepository TenantLicenseModules { get; }
        public ITwoFactorChallengeRepository TwoFactorChallenges { get; }
        public IOAuthStateRepository OAuthStates { get; }
        public ISecurityAuditLogRepository SecurityAuditLogs { get; }
        public ILoginAttemptRepository LoginAttempts { get; }
        public IDeviceSessionRepository DeviceSessions { get; }
        public ITokenBlacklistRepository TokenBlacklist { get; }
        public IPasswordHistoryRepository PasswordHistories { get; }
        public IApiKeyRepository ApiKeys { get; }
        public IWebhookEndpointRepository WebhookEndpoints { get; }
        public ERP.Identity.Application.Interfaces.Services.ITokenRevocationQueue Revocations { get; }

        // S4a (SEC-M8, TokenRevocation.md): revocations of this unit of work, saved in the same commit, published after it.
        private readonly List<ERP.Identity.Domain.Entities.RevocationOutboxMessage> _committedRevocations = [];

        public async Task<int> SaveChangesAsync(CancellationToken ct)
        {
            StageRevocations();
            int result;
            try
            {
                result = await _dbContext.SaveChangesAsync(ct);
            }
            catch when (_currentTrans == null)
            {
                // Nothing was committed: forget the rows so a later save of this scope cannot commit them alone.
                foreach (var row in _committedRevocations)
                    _dbContext.Entry(row).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                _committedRevocations.Clear();
                throw;
            }
            // Outside a transaction the change is committed now; inside one, wait for Commit.
            if (_currentTrans == null)
                await PublishCommittedRevocationsAsync(ct);
            return result;
        }

        /// <summary>The pending revocations become outbox rows of THIS save: they commit (or roll back) with the change.</summary>
        private void StageRevocations()
        {
            var rows = Revocations.Stage();
            if (rows.Count == 0)
                return;
            _dbContext.RevocationOutbox.AddRange(rows);
            _committedRevocations.AddRange(rows);
        }

        /// <summary>
        /// Fast path after the commit: publish the rows and record the result. Never throws - the change is committed, and a row
        /// that is not marked published (store or database down) is picked up by the RevocationOutboxPublisher.
        /// </summary>
        private async Task PublishCommittedRevocationsAsync(CancellationToken ct)
        {
            if (_committedRevocations.Count == 0)
                return;
            var rows = _committedRevocations.ToList();
            _committedRevocations.Clear();

            await Revocations.PublishAsync(rows, ct);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // The publish state is only bookkeeping: an unrecorded success is published again (idempotent), an unrecorded
                // failure is retried. Nothing to undo.
            }
        }
        public async Task BeginTransactionAsync(CancellationToken ct)
        {
            _currentTrans = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitTransactionAsync(CancellationToken ct = default)
        {
            try
            {
                StageRevocations();
                await _dbContext.SaveChangesAsync(ct);
                if (_currentTrans != null) await _currentTrans.CommitAsync(ct);
                _currentTrans?.Dispose();
                _currentTrans = null;
                await PublishCommittedRevocationsAsync(ct);
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
            Revocations.Clear();
            // Their rows roll back with the transaction; never publish them.
            foreach (var row in _committedRevocations)
                _dbContext.Entry(row).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            _committedRevocations.Clear();
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
