using ERP.LOG.Application.Handlers;
using ERP.LOG.Infrastructure;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Versioning;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogLogging("LoggingService");

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<GetLogsQueryHandler>());

builder.Services.AddInfrastructure(builder.Configuration);

// Real JWT validation — previously this service had no authentication wired up at
// all (no AddErpJwtAuthentication, no UseAuthentication), so the log-ingestion/query
// endpoints (exception stack traces, correlation IDs from every other service) were
// fully readable with no credential; see AuthenticationExtensions.AddErpJwtAuthentication
// / JwtClaimsUserContextService for the shared pattern every other service uses.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddErpAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
builder.Services.AddScoped<UserContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Logging Service API",
        Version = "v1",
        Description = "Centralized log management (Information/Warning/Error/Exception) across backend services and frontend apps"
    });
});

builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("*")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseGlobalExceptionHandling();

// API documentation is for developers only: never expose the endpoint list in Production.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
