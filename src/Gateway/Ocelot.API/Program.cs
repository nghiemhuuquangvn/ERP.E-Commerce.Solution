using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.API.Authorization;
using ERP.Shared.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add Ocelot
builder.Services.AddOcelot(builder.Configuration);

// Add Authentication
var identityServerUrl = builder.Configuration["IdentityServer:Authority"] ?? "http://localhost:5001";
var audience = builder.Configuration["IdentityServer:Audience"] ?? "api";

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = identityServerUrl;
        options.Audience = audience;
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

// Add Authorization
builder.Services.AddAuthorization();

// Add controllers
builder.Services.AddControllers();

// Add logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

var app = builder.Build();

// Use authentication & authorization
app.UseAuthentication();
app.UseAuthorization();

// Custom gateway authorization middleware
app.UseMiddleware<GatewayAuthorizationMiddleware>();

// Use Ocelot
app.UseOcelot().Wait();

app.MapControllers();

app.Run();
