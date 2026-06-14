using ERP.PO.Application.Mappings;
using ERP.PO.Application.Features.PurchaseOrders.Validators;
using ERP.PO.Application.Features.Suppliers.Validators;
using ERP.PO.Application.Caching;
using ERP.Shared.Authorization;
using ERP.PO.Infrastructure.Data;
using ERP.PO.Domain.Entities;
using ERP.Shared.Infrastructure.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    AppDomain.CurrentDomain.GetAssemblies()));

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

// Shared Authorization Services (from API Gateway)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, HeaderUserContextService>();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Middleware to map gateway headers to user context
app.UseMiddleware<UserContextHeaderMiddleware>();

app.MapControllers();

app.Run();
