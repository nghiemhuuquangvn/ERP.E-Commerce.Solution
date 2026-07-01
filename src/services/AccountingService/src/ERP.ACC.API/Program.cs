using ERP.ACC.API.Middleware;
using ERP.ACC.Application;
using ERP.ACC.Infrastructure;
using ERP.Shared.Authorization;
using ERP.Shared.Localization;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AccountingService");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Service", "AccountingService")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [ACC] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/acc-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning));

    builder.Services.AddCors(options =>
        options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    builder.Services.AddHealthChecks();
    builder.Services.AddControllers();
    builder.Services.AddSharedLocalization(builder.Configuration);

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddScoped<UserContext>();

    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(c =>
        c.SwaggerDoc("v1", new() { Title = "Accounting Service API", Version = "v1" }));

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseSharedLocalization();
    app.UseUserContextHeaderMiddleware();
    app.UseCors("AllowAll");
    app.UseAuthorization();
    app.MapHealthChecks("/health");
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AccountingService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
