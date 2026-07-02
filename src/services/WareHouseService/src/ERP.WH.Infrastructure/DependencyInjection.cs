using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
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

        return services;
    }
}
