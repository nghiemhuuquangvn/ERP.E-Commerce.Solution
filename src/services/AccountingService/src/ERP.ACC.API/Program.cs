using ERP.ACC.API.Middleware;
using ERP.ACC.Application;
using ERP.ACC.Infrastructure;
using ERP.Shared.Authorization;
using ERP.Shared.Authorization.ServiceAuth;
using ERP.Shared.Extensions;
using ERP.Shared.Localization;
using ERP.Shared.Versioning;
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
    builder.Services.AddErpApiVersioning();
    builder.Services.AddSharedLocalization(builder.Configuration);

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    // Real JWT validation — previously this service trusted the Gateway's forwarded
    // X-User-*/X-Tenant-* headers with no signature verification; see
    // AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService.
    builder.Services.AddErpJwtAuthentication(builder.Configuration);
    builder.Services.AddErpAuthorization();
    // Tenant must hold a license for this module (claim "licensed_module"); was missing here.
    builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Accounting));
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
    // Tenant isolation for AccountingDbContext: JWT tenant, Gateway host tenant for
    // anonymous requests, or an explicit TenantScope set by the consumers.
    builder.Services.AddRequestTenantProvider();
    builder.Services.AddScoped<UserContext>();

    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(c =>
        c.SwaggerDoc("v1", new() { Title = "Accounting Service API", Version = "v1" }));

    var app = builder.Build();

    app.UseGlobalExceptionHandling();

    app.UseSerilogRequestLogging();

    // API documentation is for developers only: never expose the endpoint list in Production.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSharedLocalization();
    app.UseAuthentication();
    app.UseUserContextHeaderMiddleware();
    app.UseMiddleware<LicenseEnforcementMiddleware>();
    app.UseCors("AllowAll");
    app.UseAuthorization();
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapControllers();
    // Public keys of this service's service-to-service tokens (MasterData verifies them).
    app.MapServiceJwks();

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
