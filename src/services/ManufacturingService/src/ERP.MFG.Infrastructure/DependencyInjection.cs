using ERP.MFG.Application.Abstractions;
using ERP.MFG.Infrastructure.Clients;
using ERP.MFG.Infrastructure.Persistence;
using ERP.MFG.Infrastructure.Repositories;
using ERP.MFG.Infrastructure.Services;
using ERP.Shared.Authorization.ServiceAuth;
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
        services.AddScoped<IMfgUnitOfWork, MfgUnitOfWork>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        services.AddScoped<IPlantRepository, PlantRepository>();
        services.AddScoped<IProductionSettingRepository, ProductionSettingRepository>();
        services.AddScoped<IItemProfileRepository, ItemProfileRepository>();
        services.AddScoped<IPlantAccessRepository, PlantAccessRepository>();
        services.AddScoped<IPlantAccessService, PlantAccessService>();
        services.AddScoped<IPlantCatalog, PlantCatalog>();
        services.AddScoped<ICalendarRepository, CalendarRepository>();
        services.AddScoped<ICrewRepository, CrewRepository>();
        services.AddScoped<IReasonCodeRepository, ReasonCodeRepository>();
        services.AddScoped<IReferenceChecker, ReferenceChecker>();
        services.AddScoped<IWorkingTimeService, WorkingTimeService>();
        services.AddScoped<IBomRepository, BomRepository>();
        services.AddScoped<IRoutingRepository, RoutingRepository>();
        services.AddScoped<IProductionSettingProvider, ProductionSettingProvider>();
        services.AddSingleton<IOpenOrderGuard, NoOrdersYetGuard>();
        services.AddMemoryCache();

        // Other services, read through internal endpoints with this service's own token (fail closed, 60 s cache).
        AddClient<IProductLookupClient, ProductLookupClient>(services, configuration, "Services:ProductServiceBaseUrl", "http://productservice:5008", "product");
        AddClient<IUnitOfMeasureClient, UnitOfMeasureClient>(services, configuration, "Services:MasterDataServiceBaseUrl", "http://masterdataservice:5015", "masterdata");
        AddClient<IProductUomConversionClient, ProductUomConversionClient>(services, configuration, "Services:ProductServiceBaseUrl", "http://productservice:5008", "product");
        AddClient<IWarehouseLookupClient, WarehouseLookupClient>(services, configuration, "Services:WarehouseServiceBaseUrl", "http://warehouseservice:5004", "warehouse");
        AddClient<IEmployeeLookupClient, EmployeeLookupClient>(services, configuration, "Services:MasterDataServiceBaseUrl", "http://masterdataservice:5015", "masterdata");
        // stock reads and writes (W5): a movement touches several tables in one transaction, so it gets a longer timeout than a lookup
        AddClient<IWarehouseStockClient, WarehouseStockClient>(services, configuration, "Services:WarehouseServiceBaseUrl", "http://warehouseservice:5004", "warehouse", timeoutSeconds: 15);
        return services;
    }

    private static void AddClient<TService, TImpl>(IServiceCollection services, IConfiguration configuration, string key, string fallback, string target, int timeoutSeconds = 5)
        where TService : class where TImpl : class, TService
    {
        services.AddHttpClient<TService, TImpl>(c =>
        {
            c.BaseAddress = new Uri((configuration[key] ?? fallback).TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }).AddServiceToken(target);
    }
}
