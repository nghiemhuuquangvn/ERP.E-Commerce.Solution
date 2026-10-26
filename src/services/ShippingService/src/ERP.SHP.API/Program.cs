using Confluent.Kafka;
using ERP.SHP.Application.Handlers;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Infrastructure.BackgroundJobs;
using ERP.SHP.Infrastructure.Carriers;
using ERP.SHP.Infrastructure.EventBus;
using ERP.SHP.Infrastructure.MessageBus;
using ERP.SHP.Infrastructure.Persistence;
using ERP.SHP.Infrastructure.Repositories;
using ERP.SHP.Infrastructure.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Messaging;
using ERP.Shared.Versioning;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShippingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ShippingDb")
        ?? "Server=(local);Database=ShippingDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreateShipmentCommandHandler>());

builder.Services.AddScoped<IShipmentRepository, ShipmentRepository>();
builder.Services.AddScoped<ICarrierRepository, CarrierRepository>();
builder.Services.AddScoped<IReturnShipmentRepository, ReturnShipmentRepository>();
builder.Services.AddScoped<IShippingUnitOfWork, ShippingUnitOfWork>();
builder.Services.AddCacheService(builder.Configuration, "shp");

// Shipping Settings (System/Tenant two-tier) — storage+CRUD only for now
builder.Services.AddScoped<IShippingSettingsService, ShippingSettingsService>();

// Kafka producer + Outbox pattern for publishing integration events (shipping-topic) —
// ShippingDbContext already harvests domain events into OutboxMessages correctly, it
// was just missing the drain job.
var kafkaServers = builder.Configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
var producerConfig = new ProducerConfig { BootstrapServers = kafkaServers, AllowAutoCreateTopics = true };
builder.Services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
builder.Services.AddScoped<IMessageBus, KafkaMessageBus>();
builder.Services.AddHostedService<OutboxProcessor>();

// RabbitMQ connection — shared single connection, matching the topic exchange model
// SaleOrderService's RabbitMqMessageBus / WarehouseService's consumer already use.
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

// Inbound consumers: sales_order_events (create shipment) and warehouse_exchange
// (inventory reserved) — both RabbitMQ, previously dead handler classes.
builder.Services.AddScoped<ERP.SHP.Infrastructure.EventBus.SagaShipmentRequestProcessor>();
builder.Services.AddHostedService<SalesOrderRabbitMqConsumer>();
builder.Services.AddHostedService<WarehouseRabbitMqConsumer>();

// Carrier client — GHN when Token configured, else Null stub.
// GhnCarrierClient resolves IHttpClientFactory.CreateClient("GHN") — previously
// AddHttpClient was never called anywhere in this service, so DI would throw the
// moment GHN was actually configured (the one environment where this client matters).
builder.Services.AddHttpClient("GHN");
if (!string.IsNullOrEmpty(builder.Configuration["Ghn:Token"]))
    builder.Services.AddScoped<ICarrierClient, GhnCarrierClient>();
else
    builder.Services.AddScoped<ICarrierClient, NullCarrierClient>();

// Real JWT validation — previously trusted the Gateway's forwarded X-User-*/
// X-Tenant-* headers with no signature verification; see
// AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddErpAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
// Tenant isolation for ShippingDbContext: JWT tenant, Gateway host tenant for anonymous
// requests, or an explicit TenantScope set by the consumers.
builder.Services.AddRequestTenantProvider();
builder.Services.AddScoped<UserContext>();
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Shipping));

builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Shipping Service API", Version = "v1" }));
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseGlobalExceptionHandling();

// API documentation is for developers only: never expose the endpoint list in Production.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSharedLocalization();
app.UseAuthentication();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();
