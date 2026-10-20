using ERP.RPT.Application.Handlers;
using ERP.RPT.Application.Interfaces;
using ERP.RPT.Infrastructure.EventConsumers;
using ERP.RPT.Infrastructure.Persistence;
using ERP.RPT.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Versioning;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReportingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ReportingDb")
        ?? "Server=(local);Database=ReportingDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<GetSalesDashboardQueryHandler>());

builder.Services.AddScoped<IReportingRepository, ReportingRepository>();

// Redis cache for dashboard queries
builder.Services.AddCacheService(builder.Configuration, "rpt");

// RabbitMQ connection — needed by ReportingSalesOrderRabbitMqConsumer
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

// Kafka consumers (payment, warehouse, pos, hrm, product)
builder.Services.AddReportingKafkaConsumers();

// RabbitMQ consumer — authoritative source for SalesOrder facts
// (SaleOrderService publishes via RabbitMQ, not Kafka)
builder.Services.AddHostedService<ReportingSalesOrderRabbitMqConsumer>();

// Real JWT validation — this service previously had no authentication pipeline at
// all (no AddAuthentication/AddJwtBearer, no IUserContextService); adding it now
// for consistency with the rest of the platform and so any future [Authorize]-gated
// endpoint actually works instead of silently passing through.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddErpAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
builder.Services.AddScoped<UserContext>();
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Reporting));

builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ReportingDbContext>("reporting-db");

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI();

app.UseSharedLocalization();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
app.Run();
