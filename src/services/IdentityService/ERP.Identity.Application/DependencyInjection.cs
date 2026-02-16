using Microsoft.Extensions.DependencyInjection;
using ERP.Identity.Domain.Common;
namespace ERP.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
