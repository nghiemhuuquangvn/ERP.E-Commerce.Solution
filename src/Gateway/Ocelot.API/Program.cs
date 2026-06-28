using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.API.Authorization;
using Ocelot.API.Monitoring;
using ERP.Shared.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Ocelot API Gateway");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Service", "Gateway")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [GW] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/gateway-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
        .MinimumLevel.Override("Ocelot", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning));

    // Configuration
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();

    // Ocelot
    builder.Services.AddOcelot(builder.Configuration);

    // Authentication
    var identityServerUrl = builder.Configuration["IdentityServer:Authority"] ?? "http://localhost:5001";

    builder.Services
        .AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            options.Authority = identityServerUrl;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = false,
                ClockSkew = TimeSpan.FromSeconds(60)
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();

    // Monitoring services (singleton — hold metrics across requests)
    builder.Services.AddSingleton<RequestMetricsStore>();
    builder.Services.AddSingleton<SystemHealthService>();
    builder.Services.AddHttpClient("health")
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(5));

    // Health checks
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.GetLevel = (ctx, _, ex) => ex != null || ctx.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : ctx.Response.StatusCode >= 400 ? LogEventLevel.Warning : LogEventLevel.Debug;
    });

    // Metrics tracking middleware — before Ocelot so we track all requests
    app.UseMiddleware<RequestMetricsMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<GatewayAuthorizationMiddleware>();

    // Monitoring + health endpoints (handled before Ocelot forwarding)
    app.MapControllers();
    app.MapHealthChecks("/health");

    // Ocelot — forwards all other requests downstream
    await app.UseOcelot();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
