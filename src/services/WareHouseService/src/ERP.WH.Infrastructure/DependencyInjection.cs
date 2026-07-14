using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using ERP.WH.Infrastructure.Persistence;
using ERP.WH.Infrastructure.EventBus;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Commands.Locations;
using ERP.WH.Application.Commands.Transfers;
using ERP.WH.Application.Commands.Stocktakes;
using ERP.WH.Application.Interfaces;
using ERP.Shared.Infrastructure.Caching;

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
        services.AddScoped<IInventoryLotRepository, InventoryLotRepository>();
        services.AddScoped<IInventorySerialRepository, InventorySerialRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ITransferOrderRepository, TransferOrderRepository>();
        services.AddScoped<IStocktakeRepository, StocktakeRepository>();
        services.AddScoped<IWarehouseUnitOfWork, WarehouseUnitOfWork>();
        services.AddScoped<IEventPublisher, EventPublisher>();

        // Inventory Settings (System/Tenant two-tier) — storage+CRUD only for now
        services.AddScoped<IInventorySettingsService, Services.InventorySettingsService>();

        services.AddCacheService(configuration, "wh");

        // RabbitMQ connection — previously never registered, so EventPublisher (which
        // requires IConnection) would throw at DI-resolution time the moment any
        // handler tried to publish. Single shared connection, matching the topic
        // exchange model SaleOrderService's RabbitMqMessageBus already uses.
        services.AddSingleton<IConnection>(_ =>
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest",
                VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
                DispatchConsumersAsync = true
            };
            return factory.CreateConnection();
        });

        // Consumes SaleOrderService's sales_order_events topic exchange (RabbitMQ —
        // SaleOrderService does not publish via Kafka) so the existing
        // SalesOrderCreatedEventHandler/SalesOrderCancelledEventHandler/
        // SalesOrderShippedEventHandler (Application/EventHandlers/*.cs) — previously
        // dead code, since nothing ever called IMediator.Publish with these types —
        // actually fire.
        services.AddHostedService<SalesOrderRabbitMqConsumer>();

        // Consumes PurchaseOrderService's "purchasing-topic" (Outbox + Kafka,
        // Newtonsoft TypeNameHandling.All — dispatch by "$type" substring) so
        // PurchaseOrderReceivedDomainEvent actually drives a real goods receipt here,
        // replacing the previous PurchaseOrderCreatedEventHandler dead-code path that
        // fired (if anything had ever published it, which nothing did) on PO
        // *creation* rather than actual receipt, and wrote to a no-op stub repository.
        services.AddHostedService<PurchaseOrderReceivedConsumer>();

        return services;
    }
}
