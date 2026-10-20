using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ERP.Shared.Authorization;

/// <summary>
/// Wires up real JWT signature/expiry validation for a downstream service, using the
/// same symmetric key/issuer/audience IdentityService issues tokens with (see
/// IdentityService's ERP.Identity.Api/Program.cs and TokenService.cs). Until this was
/// added, every non-Identity service trusted the Gateway's forwarded X-User-Id/
/// X-Tenant-Id/X-User-Roles/X-User-Permissions headers with zero cryptographic
/// verification — anyone with direct network access to a service, bypassing the
/// Gateway, could forge those headers and impersonate any user/tenant/role. Pair
/// this with JwtClaimsUserContextService (which reads the now-validated
/// HttpContext.User claims instead of headers) and call app.UseAuthentication()
/// before app.UseAuthorization() in the service's request pipeline.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddErpJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Configuration value 'Jwt:Key' is missing.");
        JwtKeyGuard.EnsureUsable(jwtKey, JwtKeyGuard.IsDevelopmentFromEnvironmentVariables());
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Configuration value 'Jwt:Issuer' is missing.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Configuration value 'Jwt:Audience' is missing.");

        services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                // Without this, ASP.NET Core's default inbound claim map silently
                // renames the token's "sub"/"email" claims to the legacy ClaimTypes.*
                // URIs, so a bare HttpContext.User.FindFirst("sub") returns null even
                // though the token is valid. Off so HttpContext.User's claim types
                // match TokenService's literal claim strings exactly (mirrors the
                // Gateway's own Program.cs, which already sets this for the same reason).
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };

                // SignalR's browser transport can't set an Authorization header on the
                // WebSocket upgrade request, so its JS client appends "?access_token=" to
                // the connection URL instead (standard SignalR+JWT pattern). JwtBearerHandler
                // only reads the header by default; this reads the query string as a fallback
                // but ONLY under "/hubs" so every other existing authenticated route on every
                // other service keeps working exactly as before. Currently only
                // ChatBotService's ChatHub lives under that path.
                //
                // The query value is expected to be a short IHubTicketStore ticket, not the
                // raw JWT — this platform's tokens can carry 240+ permission claims (10KB+),
                // which blows past Kestrel's own default 8KB MaxRequestLineSize before any
                // external proxy even sees the request (a WebSocket handshake 414). If no
                // ticket store is registered (services other than ChatBotService, or the
                // value doesn't resolve to one — e.g. an empty token for a Storefront guest),
                // falls back to using the query value as-is so this stays backward compatible.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            var ticketStore = context.HttpContext.RequestServices.GetService<IHubTicketStore>();
                            context.Token = ticketStore != null && ticketStore.TryConsumeTicket(accessToken!, out var realToken)
                                ? realToken
                                : accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    // TEMP DIAGNOSTIC (notification bell 401 investigation, round 2) — remove
                    // once root-caused. Tagged distinctly ("HUBAUTH2") to filter cleanly out
                    // of this service's very noisy background-poller logs.
                    OnChallenge = context =>
                    {
                        if (!context.HttpContext.Request.Path.StartsWithSegments("/hubs")) return Task.CompletedTask;
                        var diagLogger = context.HttpContext.RequestServices
                            .GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?
                            .CreateLogger("HUBAUTH2");
                        diagLogger?.LogError(
                            "HUBAUTH2 negotiate REJECTED path={Path} error={Error} authFailure={AuthFailure}",
                            context.HttpContext.Request.Path.Value, context.Error,
                            context.AuthenticateFailure?.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        if (!context.HttpContext.Request.Path.StartsWithSegments("/hubs")) return Task.CompletedTask;
                        var diagLogger = context.HttpContext.RequestServices
                            .GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?
                            .CreateLogger("HUBAUTH2");
                        diagLogger?.LogError("HUBAUTH2 negotiate ACCEPTED path={Path}",
                            context.HttpContext.Request.Path.Value);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
