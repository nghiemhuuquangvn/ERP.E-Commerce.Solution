using Microsoft.Extensions.DependencyInjection;
using ERP.Identity.Domain.Common;
using ERP.Identity.Application.Interfaces;


namespace ERP.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
