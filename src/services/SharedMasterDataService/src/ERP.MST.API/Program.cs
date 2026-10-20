using ERP.MST.Application;
using ERP.MST.Infrastructure;
using ERP.Shared.Authorization;
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
    Log.Information("Starting SharedMasterDataService");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Service", "SharedMasterDataService")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [MST] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/mst-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning));

    builder.Services.AddCors(options =>
        options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    builder.Services.AddHealthChecks();
    builder.Services.AddProblemDetails();
    builder.Services.AddControllers();
    builder.Services.AddErpApiVersioning();
    builder.Services.AddSharedLocalization(builder.Configuration);

    // Real JWT validation — previously trusted the Gateway's forwarded X-User-*/
    // X-Tenant-* headers with no signature verification; see
    // AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService.
    builder.Services.AddErpJwtAuthentication(builder.Configuration);
    builder.Services.AddErpAuthorization();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
    builder.Services.AddScoped<UserContext>();
    builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.SharedMasterData));

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
        c.SwaggerDoc("v1", new() { Title = "Master Data Service API", Version = "v1" }));

    var app = builder.Build();

    app.UseGlobalExceptionHandling();

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseStatusCodePages();
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
    Log.Fatal(ex, "SharedMasterDataService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
