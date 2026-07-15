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
        ISystemAuthSettingsRepository SystemAuthSettings { get; }
        ITenantAuthSettingsRepository TenantAuthSettings { get; }
        ISystemGeneralSettingsRepository SystemGeneralSettings { get; }
        ITenantGeneralSettingsRepository TenantGeneralSettings { get; }
        ITenantCompanySettingsRepository TenantCompanySettings { get; }
        ISystemWorkflowSettingsRepository SystemWorkflowSettings { get; }
        ITenantWorkflowSettingsRepository TenantWorkflowSettings { get; }
        ISystemFileStorageSettingsRepository SystemFileStorageSettings { get; }
        ITenantFileStorageSettingsRepository TenantFileStorageSettings { get; }
        ISystemSchedulerSettingsRepository SystemSchedulerSettings { get; }
        ITenantSchedulerSettingsRepository TenantSchedulerSettings { get; }
        ISystemIntegrationSettingsRepository SystemIntegrationSettings { get; }
        ITenantIntegrationSettingsRepository TenantIntegrationSettings { get; }
        ISystemApiSettingsRepository SystemApiSettings { get; }
        ITenantApiSettingsRepository TenantApiSettings { get; }
        ISystemAuditSettingsRepository SystemAuditSettings { get; }
        ITenantAuditSettingsRepository TenantAuditSettings { get; }
        ISystemAdvancedSettingsRepository SystemAdvancedSettings { get; }
        ITenantAdvancedSettingsRepository TenantAdvancedSettings { get; }
        ISystemLocalizationSettingsRepository SystemLocalizationSettings { get; }
        ITenantLocalizationSettingsRepository TenantLocalizationSettings { get; }
        ISystemFeatureFlagRepository SystemFeatureFlags { get; }
        ITenantFeatureFlagOverrideRepository TenantFeatureFlagOverrides { get; }
        ITwoFactorChallengeRepository TwoFactorChallenges { get; }
        IOAuthStateRepository OAuthStates { get; }
        ISecurityAuditLogRepository SecurityAuditLogs { get; }
        ILoginAttemptRepository LoginAttempts { get; }
        IDeviceSessionRepository DeviceSessions { get; }
        ITokenBlacklistRepository TokenBlacklist { get; }
        IPasswordHistoryRepository PasswordHistories { get; }
        IApiKeyRepository ApiKeys { get; }
        Task<int> SaveChangesAsync(CancellationToken ct);
        Task BeginTransactionAsync(CancellationToken ct);
        Task CommitTransactionAsync(CancellationToken ct);
        Task RollbackTransactionAsync(CancellationToken ct);
         void Dispose();
    }
}
