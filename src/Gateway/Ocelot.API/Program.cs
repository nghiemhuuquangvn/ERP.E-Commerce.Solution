using ERP.Shared.Authorization.TokenRevocation;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.API.Authorization;
using Ocelot.API.Monitoring;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
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

    // Ocelot's own middleware chain (Authorization/Requester/RateLimiting/...) logs several
    // Information-level lines per single request — useful for local console debugging, but pure
    // noise once shipped to the centralized log store (it drowns out the one line that matters:
    // the "HTTP ... responded ..." request-completed log). Restrict it to Warning+ here.
    builder.AddSerilogLogging("Gateway", ("Ocelot", LogEventLevel.Warning));

    // Configuration
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();

    // Ocelot
    builder.Services.AddOcelot(builder.Configuration);

    // Authentication — RS256 with IdentityService's public keys (JWKS), exact issuer and
    // audience, revoked tokens refused (see GatewayJwtBearer / ErpJwtBearerOptions).
    builder.Services
        .AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options => GatewayJwtBearer.Configure(options, builder.Configuration));

    builder.Services.AddTokenRevocation(builder.Configuration);
    builder.Services.AddAuthorization();
    builder.Services.AddControllers();

    // Monitoring services (singleton — hold metrics across requests)
    builder.Services.AddSingleton<RequestMetricsStore>();
    builder.Services.AddSingleton<SystemHealthService>();
    builder.Services.AddHttpClient("health")
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(5));

    // Tenant-from-host resolution (IdentityService owns tenants + their domains).
    builder.Services.AddTenantHostResolver(builder.Configuration);

    // Health checks
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseGlobalExceptionHandling();

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

    // Short-circuit requests that matched a local MVC endpoint (monitoring controller,
    // health checks) so Ocelot's terminal middleware never intercepts them.
    // UseRouting (auto-added by WebApplication) already set the endpoint on HttpContext;
    // UseAuthentication + UseAuthorization above already enforced [Authorize].
    app.Use(async (context, next) =>
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            await endpoint.RequestDelegate!(context);
            return;
        }
        await next(context);
    });

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
