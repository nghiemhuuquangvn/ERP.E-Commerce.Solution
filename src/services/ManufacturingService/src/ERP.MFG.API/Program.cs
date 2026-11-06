using ERP.MFG.Application;
using ERP.MFG.Infrastructure;
using ERP.Shared.Authorization;
using ERP.Shared.Authorization.ServiceAuth;
using ERP.Shared.Extensions;
using ERP.Shared.Versioning;

var builder = WebApplication.CreateBuilder(args);
builder.AddSerilogLogging("ManufacturingService");

builder.Services.AddManufacturingInfrastructure(builder.Configuration);
builder.Services.AddManufacturingApplication();

// Same authentication / authorization / tenant plumbing as every other service.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddErpAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
builder.Services.AddRequestTenantProvider();
builder.Services.AddScoped<UserContext>();
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Manufacturing));
// This service's own signing key for service-to-service tokens (published at /internal/jwks.json); the clients
// that call Product / MasterData / Warehouse add the token with AddServiceToken (Phase 0 M1).
builder.Services.AddServiceTokenIssuer();

// Requests accept enum names or numbers; responses carry names.
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddErpApiVersioning();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Manufacturing Service API", Version = "v1" }));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:5174" };
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseErpRequestLogging();
app.UseGlobalExceptionHandling();

// API documentation is for developers only: never expose the endpoint list in Production.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseMiddleware<UserContextHeaderMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseAuthorization();
app.MapControllers();
// Public keys of this service's service-to-service tokens (Identity verifies them).
app.MapServiceJwks();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
