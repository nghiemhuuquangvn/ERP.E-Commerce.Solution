using ERP.PO.Application.Mappings;
using ERP.PO.Application.Features.PurchaseOrders.Validators;
using ERP.PO.Application.Features.Suppliers.Validators;
using ERP.PO.Application.Caching;
using ERP.Shared.Authorization;
using ERP.Shared.Localization;
using ERP.PO.Infrastructure.Data;
using ERP.PO.Infrastructure.BackgroundJobs;
using ERP.PO.Infrastructure.MessageBus;
using ERP.PO.Domain.Entities;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

// Caching Configuration
var cacheSettings = builder.Configuration.GetSection("CacheSettings");
var cacheType = cacheSettings["Type"] ?? "Memory"; // Memory or Redis

if (cacheType.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    var redisConnection = cacheSettings["RedisConnection"] ?? "localhost:6379";
    var redis = ConnectionMultiplexer.Connect(redisConnection);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
    builder.Services.AddSingleton<ICacheService>(sp =>
        new RedisCacheService(sp.GetRequiredService<IConnectionMultiplexer>()));
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
}

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
