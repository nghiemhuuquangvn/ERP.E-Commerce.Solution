using ERP.PRD.Application;
using ERP.PRD.Infrastructure;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Localization;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogLogging("ProductService");

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Real JWT validation — this service previously had no authentication pipeline at
// all (no AddAuthentication/AddJwtBearer, no IUserContextService); adding it now
// for consistency with the rest of the platform and so any future [Authorize]-gated
// endpoint actually works instead of silently passing through.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product Service API",
        Version = "v1",
        Description = "Product catalog and pricing management microservice",
        Contact = new OpenApiContact
        {
            Name = "ERP E-Commerce Solution Team",
            Email = "support@erp-ecommerce.local"
        }
    });
});

builder.Services.AddControllers();
builder.Services.AddSharedLocalization(builder.Configuration);
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

app.UseSwagger();
app.UseSwaggerUI();

app.UseSharedLocalization();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
