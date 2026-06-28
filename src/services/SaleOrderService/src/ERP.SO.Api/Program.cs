using ERP.SO.Application.Handlers;
using ERP.SO.Application.Validators;
using ERP.SO.Infrastructure;
using ERP.SO.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Caching;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting SaleOrderService");
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSerilogLogging("SaleOrderService");

    // Database
    builder.Services.AddDbContext<SalesOrderDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("SalesDb")
            ?? "Server=(local);Database=SalesDB;Trusted_Connection=true;"));

    // Cache
    builder.Services.AddCacheService(builder.Configuration, "so");

    // MediatR + Validators
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(CreateOrderCommandHandler)));
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(CreateOrderCommandValidator));

    // Repository & Message Bus
    builder.Services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
    builder.Services.AddScoped<IMessageBus, RabbitMqMessageBus>();

    // Authorization & User Context
    builder.Services.AddScoped<UserContext>();
    builder.Services.AddMiddleware();

    // API
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddCors(options =>
        options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    var app = builder.Build();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.GetLevel = (ctx, elapsed, ex) =>
            ex != null ? LogEventLevel.Error
            : ctx.Response.StatusCode >= 500 ? LogEventLevel.Error
            : ctx.Response.StatusCode >= 400 ? LogEventLevel.Warning
            : LogEventLevel.Information;
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseDeveloperExceptionPage();
    }

    app.UseHttpsRedirection();
    app.UseUserContextHeaderMiddleware();
    app.UseCors("AllowAll");
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SaleOrderService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
