using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Localization;
using ERP.Shared.Versioning;
using ERP.WH.Application;
using ERP.WH.Infrastructure;
using Serilog;
using Serilog.Events;
using ERP.Shared.Authorization.ServiceAuth;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting WareHouseService");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Service", "WareHouseService")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [WH] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/wh-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
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

    // Real JWT validation — this service previously had no authentication pipeline
    // at all (no AddAuthentication/AddJwtBearer, no IUserContextService); adding it
    // now for consistency with the rest of the platform and so any future
    // [Authorize]-gated endpoint actually works instead of silently passing through.
    builder.Services.AddErpJwtAuthentication(builder.Configuration);
    // /internal endpoints: service tokens of ServiceAuth:TrustedCallers (SaleOrder).
    builder.Services.AddErpServiceAuthentication(builder.Configuration);
    builder.Services.AddErpAuthorization();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
    // Tenant isolation for WarehouseDbContext: JWT tenant, Gateway host tenant for anonymous
    // requests, or an explicit TenantScope set by consumers and the reservation-expiry job.
    builder.Services.AddRequestTenantProvider();
    builder.Services.AddScoped<UserContext>();
    builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Warehouse));

    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen();

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
    app.UseCors("AllowAll");
    app.UseAuthentication();
    app.UseMiddleware<UserContextHeaderMiddleware>();
    app.UseMiddleware<LicenseEnforcementMiddleware>();
    app.UseAuthorization();
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WareHouseService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
