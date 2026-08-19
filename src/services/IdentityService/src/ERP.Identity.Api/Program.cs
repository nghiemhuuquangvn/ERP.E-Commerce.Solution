using ERP.Identity.Api.Authorization;
using ERP.Identity.Application;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Infrastructure;
using ERP.Identity.Infrastructure.OpenIddict;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Repositories;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using static OpenIddict.Abstractions.OpenIddictConstants;

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

builder.Services.AddControllers();
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

// OpenIddict OAuth2/OIDC server — client_credentials (Part A1), Authorization
// Code+PKCE (Part A2), and discovery/userinfo + asymmetric signing (Part A3) of
// CommonServiceRoadmap.md §1. OpenIddict requires an absolute URI for SetIssuer;
// "ERP.Identity" (the existing plain-string Jwt:Issuer value) isn't one, so it's
// wrapped as a urn: URI here rather than introducing a new config key — both the
// original string and this urn: form are accepted below.
var oidcIssuer = new Uri($"urn:{builder.Configuration["Jwt:Issuer"]}", UriKind.Absolute);

// A3: real asymmetric signing, replacing A0-A2's AddEphemeralSigningKey() stopgap
// (which regenerated a new key — and so orphaned every previously-issued token and
// any cached JWKS — on every process restart). Self-managed rather than OpenIddict's
// own AddDevelopmentSigningCertificate() specifically so the SAME certificate's
// public key can be added to the "Bearer" JwtBearer scheme's IssuerSigningKeys below —
// OpenIddict's helper manages the certificate entirely internally with no way to get
// it back out. Still a development/local stopgap, not production PKI (self-signed, no
// rotation, no Key Vault/HSM) — flagged as a follow-up decision, not designed further
// here.
var signingCertificate = DevSigningCertificateProvider.GetOrCreate();

builder.Services.AddOpenIddict()
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.SetUserInfoEndpointUris("/connect/userinfo");
        options.SetConfigurationEndpointUris("/.well-known/openid-configuration");
        options.SetJsonWebKeySetEndpointUris("/.well-known/jwks.json");
        options.AllowClientCredentialsFlow();
        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();
        options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email);

        options.SetIssuer(oidcIssuer);

        // All OpenIddict-issued tokens (access + ID) are now signed with this one
        // asymmetric certificate — no more shared-HMAC-secret access tokens (A1/A2's
        // flagged debt). AddDevelopmentSigningCertificate()/AddSigningCertificate()
        // implicitly registers RS256; ID tokens already required this per the OIDC
        // spec (unchanged from A2), access tokens are the actual cutover here.
        options.AddSigningCertificate(signingCertificate);

        // AES encryption keys must be exactly 256 bits; Jwt:Key isn't guaranteed to be
        // (it's sized for HMAC-SHA256 signing, which accepts any length >= 128 bits) —
        // derive a 256-bit key from it via SHA-256 rather than adding a second config
        // value. Only used for refresh/authorization-code tokens (encrypted by
        // default); access tokens are exempted below.
        options.AddEncryptionKey(new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(jwtKey))));
        // Emit a plain signed JWT access token (not OpenIddict's default encrypted
        // format) so the existing "Bearer" JwtBearer scheme below can validate it with
        // no changes.
        options.DisableAccessTokenEncryption();

        options.AddEventHandler(ClientCredentialsClaimsHandler.Descriptor);
        options.AddEventHandler(UserinfoClaimsHandler.Descriptor);

        // Every other service in this platform runs plain HTTP behind the Gateway,
        // which is the actual TLS termination point (see AuthController's own
        // endpoints, which have no such requirement) — OpenIddict defaults to
        // rejecting non-HTTPS requests, which would be inconsistent with that
        // convention, so it's disabled here to match.
        //
        // Authorization endpoint passthrough: OidcAuthorizationController owns
        // /connect/authorize (needs to render a login page / drive the tenant-picker
        // redirect chain) — unlike the token/userinfo/discovery/jwks endpoints above,
        // which OpenIddict still handles entirely itself (no controller involved).
        options.UseAspNetCore()
            .DisableTransportSecurityRequirement()
            .EnableAuthorizationEndpointPassthrough();
    });

// Two schemes: the default JWT bearer (users) and a second "ApiKey" scheme
// (service-to-service callers presenting an X-Api-Key header — see
// ApiKeyAuthenticationHandler). A policy scheme picks between them per-request so
// every existing [Authorize]/[AuthorizePermission] usage keeps working unchanged —
// no controller needs an explicit AuthenticationSchemes list.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "SmartAuth";
    options.DefaultAuthenticateScheme = "SmartAuth";
    options.DefaultChallengeScheme = "SmartAuth";
})
    .AddPolicyScheme("SmartAuth", "JWT or API Key", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(ApiKeyAuthenticationSchemeOptions.HeaderName)
                ? ApiKeyAuthenticationHandler.SchemeName
                : "Bearer";
    })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null)
    .AddJwtBearer("Bearer", options =>
    {
        // Without this, ASP.NET Core's default inbound claim map silently renames
        // "sub"/"email" to the legacy ClaimTypes.* URIs, so bare
        // HttpContext.User.FindFirst("sub") calls (ControllerTenantExtensions,
        // AuthorizePermissionAttribute's IUserContextService, several controllers) return
        // null even for a valid token. Off so claim types match TokenService's literal
        // claim strings.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Two valid issuers: the proprietary TokenService's plain-string issuer
            // ("ERP.Identity", used by /api/auth/login), and the urn: form OpenIddict
            // emits for client_credentials tokens (see oidcIssuer above) — both mint
            // tokens signed with the same symmetric key, so both are accepted here.
            ValidIssuers = [builder.Configuration["Jwt:Issuer"], oidcIssuer.ToString()],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            // Two valid signing keys: the symmetric Jwt:Key (still what TokenService's
            // proprietary /api/auth/login flow signs with — A2 explicitly kept that
            // unchanged), and the A3 signing certificate's public key (what OpenIddict
            // now signs every access/ID token with, per the asymmetric cutover above).
            IssuerSigningKeys =
            [
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                new X509SecurityKey(signingCertificate)
            ]
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
    })
    // Interactive-session cookie for Part A2's Authorization Code+PKCE flow only
    // (OidcAuthorizationController) — NOT part of the SmartAuth default-scheme chain
    // above, so it has zero effect on any existing API endpoint; only ever referenced
    // explicitly by name (OidcInteractiveAuthenticationDefaults.Scheme).
    .AddCookie(OidcInteractiveAuthenticationDefaults.Scheme, options =>
    {
        options.Cookie.Name = "erp_identity_oidc";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        // No LoginPath — OidcAuthorizationController itself redirects to /connect/login
        // when it finds no cookie, rather than relying on the Challenge()/LoginPath
        // convention, since it needs to preserve the full original /connect/authorize
        // query string as a ReturnUrl regardless of which action triggered the redirect.
    });

// Fine-grained permission checks are handled by ERP.Shared.Authorization's
// [AuthorizePermission(...)] action filter (claims-based, no DB round trip) — the
// same mechanism the other 13 services use. AddAuthorization() is still required so
// bare [Authorize] (self-service endpoints with no specific permission) keeps working.
builder.Services.AddAuthorization();

// AuthorizePermissionAttribute resolves IUserContextService from the request's DI
// container and treats a missing registration as "not authenticated" (401, no
// WWW-Authenticate header — easy to mistake for a JWT validation failure). Every
// other service already registers this (+ its IHttpContextAccessor dependency) in
// its own Program.cs; the Phase 2 migration that moved Identity's controllers onto
// [AuthorizePermission] missed adding both here, so every migrated endpoint either
// 401'd (missing IUserContextService) or 500'd (missing IHttpContextAccessor)
// regardless of the caller's actual permissions.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();

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

// Dev/test-only: seeds one client_credentials OpenIddictApplication so Part A1's flow
// is exercisable without a real client-management API (not yet built — see
// OpenIddictClientSeeder's doc comment). Never runs unattended in Production.
if (app.Environment.IsDevelopment())
    await OpenIddictClientSeeder.SeedAsync(app.Services);

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
