using ERP.Identity.Api.Authorization;
using ERP.Identity.Application;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Infrastructure;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Repositories;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Extensions;
using ERP.Shared.Localization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogLogging("IdentityService");

// Database
builder.Services.AddDbContext<AuthDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("AuthDB")));

// DI
try
{
    builder.Services.AddInfrastructure();
}
catch (System.Reflection.ReflectionTypeLoadException ex)
{
    foreach (var loaderException in ex.LoaderExceptions ?? [])
        Console.WriteLine($"Type load error: {loaderException?.Message}");
}

builder.Services.AddControllers(options =>
{
    options.Filters.Add<IdentityExceptionFilter>();
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS — explicit allow-list from config, not AllowAnyOrigin. IdentityService issues
// tokens for every other service, so a permissive CORS policy here is a
// higher-value target than on a downstream service.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("IdentityCors", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader());
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Configuration value 'Jwt:Key' is missing.");

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Session-management gap: a token blacklisted via /auth/logout must stop
        // working immediately, not just at its natural expiry. Only enforced here,
        // on Identity's own JWT validation — other services trust the gateway's
        // injected headers rather than re-validating the JWT themselves, so a
        // blacklisted token still works against them today (a known, documented
        // limitation, not something this hook can fix from Identity alone).
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(jti))
                    return;

                var sessionService = context.HttpContext.RequestServices.GetRequiredService<ISessionService>();
                if (await sessionService.IsTokenBlacklistedAsync(jti, context.HttpContext.RequestAborted))
                    context.Fail("Token has been revoked.");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Permission", policy =>
        policy.Requirements.Add(new PermissionRequirement()));
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseSharedLocalization();

app.UseRouting();
app.UseCors("IdentityCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
