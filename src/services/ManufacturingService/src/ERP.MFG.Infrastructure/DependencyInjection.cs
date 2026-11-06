using ERP.MFG.Application.Abstractions;
using ERP.MFG.Infrastructure.Persistence;
using ERP.MFG.Infrastructure.Services;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.MFG.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionName = "ManufacturingDb";

    public static IServiceCollection AddManufacturingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connection))
            throw new ServiceMisconfiguredException($"ConnectionStrings:{ConnectionName}");

        services.AddDbContext<MfgDbContext>(o => o.UseNpgsql(connection, n => n.EnableRetryOnFailure(3)));
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        return services;
    }
}
