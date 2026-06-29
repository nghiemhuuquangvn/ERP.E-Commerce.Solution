using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ERP.WH.Infrastructure.Persistence;
using ERP.WH.Infrastructure.EventBus;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.EventHandlers;

namespace ERP.WH.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WarehouseDB");
        services.AddDbContext<WarehouseDbContext>(options =>
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly("ERP.WH.Infrastructure")));

        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IEventPublisher, EventPublisher>();

        services.AddScoped<IReceiptRepository, ReceiptRepository>();

        return services;
    }
}
