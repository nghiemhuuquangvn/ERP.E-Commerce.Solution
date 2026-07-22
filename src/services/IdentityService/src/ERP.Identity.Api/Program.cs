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
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogLogging("IdentityService");

// Database
builder.Services.AddDbContext<AuthDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("AuthDB")));

// Settings read-through cache (Memory by default; set Cache:Type=Redis to switch)
builder.Services.AddCacheService(builder.Configuration, "identity");

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
builder.Services.AddErpApiVersioning();
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

// This key signs tokens accepted by every one of the 14 downstream services (see
// ERP.Shared.Authorization.AuthenticationExtensions) — a short/weak value is
// brute-forceable and would forge valid tokens platform-wide, so fail fast rather
// than starting up with an insecure signing key. 32 bytes/UTF-8 chars = 256 bits,
// matching HmacSha256's expected key size.
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Configuration value 'Jwt:Key' must be at least 32 bytes (256 bits) to be a secure HMAC-SHA256 signing key.");

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        // Without this, ASP.NET Core's default inbound claim map silently renames
        // "sub"/"email" to the legacy ClaimTypes.* URIs, so bare
        // HttpContext.User.FindFirst("sub") calls (ControllerTenantExtensions,
        // PermissionAuthorizationHandler, several controllers) return null even for a
        // valid token. Off so claim types match TokenService's literal claim strings.
        options.MapInboundClaims = false;
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
        // working immediately, not just at its natural expiry. All 13 downstream
        // services now independently validate JWT signature/expiry too (via
        // AddErpJwtAuthentication), but none of them check TokenBlacklist — that
        // check only runs here, on Identity's own JWT validation. A blacklisted
        // token's signature/expiry are still valid, so it keeps working against
        // every other service until it expires naturally (known, documented
        // limitation — needs a shared revocation store to close fully).
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

// Brute-force protection for the auth endpoints. This is on top of, not instead of,
// the per-(ip,email) failed-attempt counter in LoginAttemptRepository — that guard
// only scopes to one (ip,email) pair, so it doesn't stop an attacker spraying
// attempts across many different accounts from one IP. Rate limits are keyed by
// client IP (X-Forwarded-For first, matching AuthController.GetClientIp()).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    string ResolveClientIp(HttpContext context) =>
        context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrEmpty(forwarded)
            ? forwarded.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("auth-sensitive", context =>
        RateLimitPartition.GetFixedWindowLimiter(ResolveClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Tighter: a 6-digit 2FA code has only 1,000,000 possibilities — this is the
    // control that actually prevents brute-forcing it, since TwoFactorChallenge's
    // own MaxAttempts only caps attempts against one challenge, not new challenges.
    options.AddPolicy("auth-2fa-verify", context =>
        RateLimitPartition.GetFixedWindowLimiter(ResolveClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

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
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseSharedLocalization();

app.UseRouting();
app.UseCors("IdentityCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
