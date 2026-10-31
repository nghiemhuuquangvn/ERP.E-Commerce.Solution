using Microsoft.Extensions.DependencyInjection;
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
using ERP.Shared.Security;
using ERP.Identity.Infrastructure.Security;

namespace ERP.Identity.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddHttpClient();

            // Outbound webhook delivery to tenant-configured URLs (SSRF-sensitive): no
            // redirects (a public URL can't bounce to an internal one), short timeout, and a
            // ConnectCallback that re-checks the ACTUALLY resolved IPs at connect time, so a
            // public-looking hostname resolving/rebinding to an internal address is refused.
            services.AddHttpClient("webhook", client => client.Timeout = TimeSpan.FromSeconds(10))
                .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectCallback = async (context, ct) =>
                    {
                        var addresses = await System.Net.Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                        if (addresses.Length == 0 || addresses.Any(WebhookUrlRules.IsBlockedAddress))
                            throw new System.Net.Http.HttpRequestException(
                                $"Webhook host '{context.DnsEndPoint.Host}' resolves to a blocked (internal/private) address.");

                        var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp) { NoDelay = true };
                        try
                        {
                            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }
                });

            // OpenIddict OAuth2/OIDC server core — persistence-layer plumbing bound to
            // AuthDbContext, same rationale as the repository registrations below.
            // .AddServer(...) (endpoints, flows, signing) is configured separately in
            // ERP.Identity.Api/Program.cs, inline, matching this service's existing
            // convention of configuring auth schemes there rather than via a shared
            // extension. See CommonServiceRoadmap.md §1 / Part A0.
            services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore()
                        .UseDbContext<AuthDbContext>()
                        .ReplaceDefaultEntities<Guid>();
                });

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

            // Per-unit-of-work queue publishing to the shared revocation store after commit —
            // logout/lock/password/role/license changes reach every service. The store itself
            // (AddTokenRevocation) is registered in Program.cs, which has the configuration.
            services.AddScoped<ITokenRevocationQueue, ERP.Identity.Infrastructure.Security.TokenRevocationQueue>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IPermissionRepository, PermissionRepository>();
            // AccessTokenSigningCredentials (from the signing certificates) is registered in Program.cs.
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
            services.AddScoped<IEmailConfirmTokenRepository, EmailConfirmTokenRepository>();
            services.AddScoped<IUserRoleResolver, UserRoleResolver>();
            services.AddScoped<IRolePermissionResolver, RolePermissionResolver>();
            services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
            services.AddScoped<ISystemAuthSettingsRepository, SystemAuthSettingsRepository>();
            services.AddScoped<ITenantAuthSettingsRepository, TenantAuthSettingsRepository>();
            services.AddScoped<ISystemGeneralSettingsRepository, SystemGeneralSettingsRepository>();
            services.AddScoped<ITenantGeneralSettingsRepository, TenantGeneralSettingsRepository>();
            services.AddScoped<ITenantCompanySettingsRepository, TenantCompanySettingsRepository>();
            services.AddScoped<ISystemWorkflowSettingsRepository, SystemWorkflowSettingsRepository>();
            services.AddScoped<ITenantWorkflowSettingsRepository, TenantWorkflowSettingsRepository>();
            services.AddScoped<ISystemFileStorageSettingsRepository, SystemFileStorageSettingsRepository>();
            services.AddScoped<ITenantFileStorageSettingsRepository, TenantFileStorageSettingsRepository>();
            services.AddScoped<ISystemSchedulerSettingsRepository, SystemSchedulerSettingsRepository>();
            services.AddScoped<ITenantSchedulerSettingsRepository, TenantSchedulerSettingsRepository>();
            services.AddScoped<ISystemIntegrationSettingsRepository, SystemIntegrationSettingsRepository>();
            services.AddScoped<ITenantIntegrationSettingsRepository, TenantIntegrationSettingsRepository>();
            services.AddScoped<ISystemApiSettingsRepository, SystemApiSettingsRepository>();
            services.AddScoped<ITenantApiSettingsRepository, TenantApiSettingsRepository>();
            services.AddScoped<ISystemAuditSettingsRepository, SystemAuditSettingsRepository>();
            services.AddScoped<ITenantAuditSettingsRepository, TenantAuditSettingsRepository>();
            services.AddScoped<ISystemAdvancedSettingsRepository, SystemAdvancedSettingsRepository>();
            services.AddScoped<ITenantAdvancedSettingsRepository, TenantAdvancedSettingsRepository>();
            services.AddScoped<ISystemLocalizationSettingsRepository, SystemLocalizationSettingsRepository>();
            services.AddScoped<ITenantLocalizationSettingsRepository, TenantLocalizationSettingsRepository>();
            services.AddScoped<ISystemFeatureFlagRepository, SystemFeatureFlagRepository>();
            services.AddScoped<ITenantFeatureFlagOverrideRepository, TenantFeatureFlagOverrideRepository>();
            services.AddScoped<IModuleDefinitionRepository, ModuleDefinitionRepository>();
            services.AddScoped<ILicensePlanRepository, LicensePlanRepository>();
            services.AddScoped<ILicensePlanModuleRepository, LicensePlanModuleRepository>();
            services.AddScoped<ITenantLicenseRepository, TenantLicenseRepository>();
            services.AddScoped<ITenantLicenseModuleRepository, TenantLicenseModuleRepository>();
            services.AddScoped<ITwoFactorChallengeRepository, TwoFactorChallengeRepository>();
            services.AddScoped<IOAuthStateRepository, OAuthStateRepository>();
            services.AddScoped<ISecurityAuditLogRepository, SecurityAuditLogRepository>();
            services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
            services.AddScoped<IDeviceSessionRepository, DeviceSessionRepository>();
            services.AddScoped<ITokenBlacklistRepository, TokenBlacklistRepository>();
            services.AddScoped<IPasswordHistoryRepository, PasswordHistoryRepository>();
            services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
            services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();

            // New services
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<ISettingsService, SettingsService>();
            services.AddScoped<IGeneralSettingsService, GeneralSettingsService>();
            services.AddScoped<ICompanySettingsService, CompanySettingsService>();
            services.AddScoped<ISecretProtector, AesSecretProtector>();
            services.AddScoped<OutboxPayloadProtector>();
            services.AddScoped<IWorkflowSettingsService, WorkflowSettingsService>();
            services.AddScoped<IFileStorageSettingsService, FileStorageSettingsService>();
            services.AddScoped<ISchedulerSettingsService, SchedulerSettingsService>();
            services.AddScoped<IIntegrationSettingsService, IntegrationSettingsService>();
            services.AddScoped<IApiSettingsService, ApiSettingsService>();
            services.AddScoped<IAuditSettingsService, AuditSettingsService>();
            services.AddScoped<IAdvancedSettingsService, AdvancedSettingsService>();
            services.AddScoped<ILocalizationSettingsService, LocalizationSettingsService>();
            services.AddScoped<IFeatureFlagService, FeatureFlagService>();
            services.AddScoped<ILicenseService, LicenseService>();
            services.AddScoped<ITwoFactorCodeGenerator, TwoFactorCodeGenerator>();
            services.AddScoped<ISecurityAuditService, SecurityAuditService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<ITenantAccessPolicy, TenantAccessPolicy>();
            services.AddScoped<ITenantDomainService, TenantDomainService>();
            services.AddScoped<IApiKeyService, ApiKeyService>();
            services.AddScoped<IWebhookEndpointService, WebhookEndpointService>();
            services.AddScoped<IWebhookSender, WebhookSender>();
            services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();

            services.AddHostedService<OutboxProcessor>();
            services.AddHostedService<WebhookSecretEncryptionJob>();

            return services;
        }
    }
} 