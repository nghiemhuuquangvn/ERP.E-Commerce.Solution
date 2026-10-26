using Confluent.Kafka;
using ERP.PRM.Application.EventHandlers;
using ERP.PRM.Application.Handlers;
using ERP.PRM.Application.Interfaces;
using ERP.PRM.Infrastructure.BackgroundJobs;
using ERP.PRM.Infrastructure.EventBus;
using ERP.PRM.Infrastructure.MessageBus;
using ERP.PRM.Infrastructure.Persistence;
using ERP.PRM.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Messaging;
using ERP.Shared.Versioning;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using ERP.Shared.Authorization.ServiceAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PromotionDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PromotionDb")
        ?? "Server=(local);Database=PromotionDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreatePromotionCommandHandler>());

builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<ILoyaltyAccountRepository, LoyaltyAccountRepository>();
builder.Services.AddScoped<IPromotionProductCacheRepository, PromotionProductCacheRepository>();
builder.Services.AddScoped<IFlashSaleRepository, FlashSaleRepository>();
builder.Services.AddScoped<IPromotionUnitOfWork, PromotionUnitOfWork>();
builder.Services.AddCacheService(builder.Configuration, "prm");

// Kafka producer + Outbox pattern for publishing integration events (promotion-topic)
var kafkaServers = builder.Configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
var producerConfig = new ProducerConfig { BootstrapServers = kafkaServers, AllowAutoCreateTopics = true };
builder.Services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
builder.Services.AddScoped<IMessageBus, KafkaMessageBus>();
builder.Services.AddHostedService<OutboxProcessor>();

// Promotion scheduler: auto-activate/expire promotions and flash sales
builder.Services.AddHostedService<PromotionSchedulerJob>();

// RabbitMQ connection for SalesOrderCreated events
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

// Inbound consumers: product-topic (Kafka) and sales_order_events (RabbitMQ)
builder.Services.AddHostedService<PromotionKafkaConsumer>();
builder.Services.AddHostedService<SalesOrderRabbitMqConsumer>();
builder.Services.AddScoped<ProductCreatedForPromotionHandler>();
builder.Services.AddScoped<SalesOrderCreatedForPromotionHandler>();
builder.Services.AddScoped<SalesOrderReversedForPromotionHandler>();

// Real JWT validation — previously trusted the Gateway's forwarded X-User-*/
// X-Tenant-* headers with no signature verification; see
// AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
// /internal endpoints: service tokens of ServiceAuth:TrustedCallers (SaleOrder).
builder.Services.AddErpServiceAuthentication(builder.Configuration);
builder.Services.AddErpAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
// Tenant isolation for PromotionDbContext: JWT tenant, the Gateway host tenant for the
// anonymous storefront (public promotions / flash sales), or an explicit TenantScope.
builder.Services.AddRequestTenantProvider();
builder.Services.AddScoped<UserContext>();
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Promotion));
builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Promotion Service API", Version = "v1" }));
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
