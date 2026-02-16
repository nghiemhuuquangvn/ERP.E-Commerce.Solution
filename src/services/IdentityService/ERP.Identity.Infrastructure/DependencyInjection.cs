using Microsoft.Extensions.DependencyInjection;
using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Infrastructure.Services;
using ERP.Identity.Application.Interfaces.External;
using ERP.Identity.Infrastructure.ExternalAuthLogin;
using ERP.Identity.Domain.Events;
using ERP.Identity.Infrastructure.Events;
using ERP.Identity.infrastructure.Events.Handlers;

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
            services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, UserRegisteredDomainEventHandler>();

            return services;
        }
    }
}