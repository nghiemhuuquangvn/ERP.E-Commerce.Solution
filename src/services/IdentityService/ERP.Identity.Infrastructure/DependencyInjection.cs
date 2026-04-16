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

namespace ERP.Identity.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IExternalAuthProvider, MicrosoftAuthProvider>();
            services.AddScoped<IExternalAuthProvider, GoogleAuthProvider>();
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
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IUserRoleResolver, UserRoleResolver>();
            services.AddScoped<IRolePermissionResolver, RolePermissionResolver>();
            services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();

            services.AddHostedService<OutboxProcessor>();

            return services;
        }
    }
}