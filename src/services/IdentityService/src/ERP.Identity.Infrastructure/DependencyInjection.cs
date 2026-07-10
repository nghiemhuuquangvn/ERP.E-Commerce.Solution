using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ERP.Identity.Infrastructure.Authorization;
using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Infrastructure.Services;
using ERP.Identity.Application.Interfaces.External;
using ERP.Identity.Infrastructure.ExternalAuthLogin;
using ERP.Identity.Domain.Events;
using ERP.Identity.Infrastructure.Events;
using ERP.Identity.infrastructure.Events.Handlers;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Repositories;
using ERP.Identity.Application.Interfaces.Mapping;
using ERP.Identity.Infrastructure.Mapping;
using ERP.Identity.Infrastructure.Configurations;
using Microsoft.Extensions.Configuration;
using ERP.Identity.Infrastructure.MessageBus;
using ERP.Identity.Infrastructure.BackgroundJobs;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace ERP.Identity.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddHttpClient();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IExternalAuthProvider, MicrosoftAuthProvider>();
            services.AddScoped<IExternalAuthProvider, GoogleAuthProvider>();
            services.AddScoped<IExternalAuthProvider, FacebookAuthProvider>();
            services.AddScoped<TwitterOAuthService>();
            services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

            services.Configure<KafkaSettings>(options => services.BuildServiceProvider().GetRequiredService<IConfiguration>().GetSection("KafkaSettings").Bind(options));
            services.AddSingleton<IProducer<string, string>>(sp => 
            {
                var settings = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;
                var config = new ProducerConfig
                {
                    BootstrapServers = settings.BootstrapServers,
                    Acks = Enum.Parse<Acks>(settings.Acks),
                    MessageSendMaxRetries = settings.Retries,
                    SocketTimeoutMs = settings.SocketTimeoutMs,
                    BatchSize = settings.BatchSize,
                    RetryBackoffMs = settings.RetryBackoffMs,
                    ReconnectBackoffMs = settings.ReconnectBackoffMs
                };
                return new ProducerBuilder<string, string>(config).Build();
            });
            services.AddSingleton<IMessageBus, KafkaMessageBus>();
            services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, UserRegisteredDomainEventHandler>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IPermissionRepository, PermissionRepository>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
            services.AddScoped<IUserRoleResolver, UserRoleResolver>();
            services.AddScoped<IRolePermissionResolver, RolePermissionResolver>();
            services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
            services.AddScoped<ISystemAuthSettingsRepository, SystemAuthSettingsRepository>();
            services.AddScoped<ITenantAuthSettingsRepository, TenantAuthSettingsRepository>();
            services.AddScoped<ITwoFactorChallengeRepository, TwoFactorChallengeRepository>();
            services.AddScoped<IOAuthStateRepository, OAuthStateRepository>();
            services.AddScoped<ISecurityAuditLogRepository, SecurityAuditLogRepository>();
            services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
            services.AddScoped<IDeviceSessionRepository, DeviceSessionRepository>();
            services.AddScoped<ITokenBlacklistRepository, TokenBlacklistRepository>();
            services.AddScoped<IPasswordHistoryRepository, PasswordHistoryRepository>();
            services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

            // New services
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<ISettingsService, SettingsService>();
            services.AddScoped<ITwoFactorCodeGenerator, TwoFactorCodeGenerator>();
            services.AddScoped<ISecurityAuditService, SecurityAuditService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<IApiKeyService, ApiKeyService>();
            services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();

            // Backs the "Permission" authorization policy (AddAuthorization in
            // Program.cs). Without this, [Authorize(Policy = "Permission")] +
            // [RequirePermission(...)] fails closed for every caller — a policy
            // requirement is only satisfied if a registered IAuthorizationHandler
            // calls context.Succeed(...), and none was ever registered.
            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

            services.AddHostedService<OutboxProcessor>();

            return services;
        }
    }
}