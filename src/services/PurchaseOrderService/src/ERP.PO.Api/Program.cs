using ERP.PO.Application.Mappings;
using ERP.PO.Application.Features.PurchaseOrders.Validators;
using ERP.PO.Application.Features.Suppliers.Validators;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Versioning;
using ERP.PO.Infrastructure.Data;
using ERP.PO.Infrastructure.BackgroundJobs;
using ERP.PO.Infrastructure.MessageBus;
using ERP.PO.Infrastructure.EventBus;
using ERP.PO.Application.Features.PurchaseRequests;
using ERP.PO.Domain.Entities;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Confluent.Kafka;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddOpenApi();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    AppDomain.CurrentDomain.GetAssemblies()));

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

// Shared Authorization Services — real JWT validation (previously this service
// trusted the Gateway's forwarded X-User-*/X-Tenant-* headers with no signature
// verification; see AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService).
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
builder.Services.AddScoped<UserContext>();
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.PurchaseOrder));

// Caching — shared Cache:Type / ConnectionStrings:Redis convention used by every other
// service (was previously its own duplicate ICacheService/RedisCacheService reading a
// different "CacheSettings:Type" config key that docker-compose/K8s never actually set,
// and which nothing in this service ever injected — dead, inconsistent infrastructure).
builder.Services.AddCacheService(builder.Configuration, "po");

builder.Services.AddLogging();
builder.Services.AddHealthChecks();

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("PurchasingDb")
    ?? "Server=localhost;Database=PurchasingDB;Trusted_Connection=true;TrustServerCertificate=true;";

builder.Services.AddDbContext<PurchasingDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "po");
        sqlOptions.CommandTimeout(30);
    }));

// Repository and Unit of Work
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork>(sp => new UnitOfWork(sp.GetRequiredService<PurchasingDbContext>()));
builder.Services.AddScoped<IPurchaseRequestRepository, PurchaseRequestRepository>();

// RabbitMQ connection — first time this service consumes anything (previously
// outbound-only via Kafka/KafkaMessageBus). Mirrors WareHouseService's own
// EventPublisher/ConnectionFactory setup (ERP.WH.Infrastructure/DependencyInjection.cs)
// since WarehouseEventRabbitMqConsumer binds to that same "warehouse_exchange".
builder.Services.AddSingleton<IConnection>(_ =>
{
    var factory = new ConnectionFactory
    {
        HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost",
        Port = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
        VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
        DispatchConsumersAsync = true
    };
    return factory.CreateConnection();
});

// Consumes WareHouseService's StockAlertEvent (low-stock signal) to create
// suggestion-only PurchaseRequests — see WarehouseEventRabbitMqConsumer.
builder.Services.AddHostedService<WarehouseEventRabbitMqConsumer>();

// Kafka producer + Outbox pattern for publishing integration events (purchasing-topic)
var kafkaServers = builder.Configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
var producerConfig = new ProducerConfig
{
    BootstrapServers = kafkaServers,
    AllowAutoCreateTopics = true
};
builder.Services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
builder.Services.AddScoped<IMessageBus, KafkaMessageBus>();
builder.Services.AddHostedService<OutboxProcessor>();

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.MapOpenApi();

app.UseSharedLocalization();

app.UseAuthentication();
// Middleware to map gateway headers to user context
app.UseMiddleware<UserContextHeaderMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
