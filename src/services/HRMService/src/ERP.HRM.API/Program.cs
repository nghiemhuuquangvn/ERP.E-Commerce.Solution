using ERP.HRM.Application;
using ERP.HRM.Infrastructure;
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
    Log.Information("Starting HRMService");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Service", "HRMService")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [HRM] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/hrm-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning));

    builder.Services.AddCors(options =>
        options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    builder.Services.AddHealthChecks();
    builder.Services.AddProblemDetails();
    builder.Services.AddControllers();
    builder.Services.AddSharedLocalization(builder.Configuration);

    // Authorization / UserContext (from API Gateway headers)
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserContextService, HeaderUserContextService>();

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "HRM Service API v1");
        });
        app.MapOpenApi();
    }

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseHttpsRedirection();
    app.UseSharedLocalization();
    app.UseCors("AllowAll");

    // Propagate user context from gateway headers
    app.UseMiddleware<UserContextHeaderMiddleware>();

    app.UseAuthorization();
    app.MapHealthChecks("/health");
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "HRMService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
