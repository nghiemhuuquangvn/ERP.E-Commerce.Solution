using ERP.Shared.Authorization.TokenRevocation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Authorization;

/// <summary>
/// Wires up real JWT signature/expiry validation for a downstream service: RS256 with
/// IdentityService's public keys (JWKS), same issuer/audience as TokenService — see
/// ErpJwtBearerOptions. Until this was
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
        // Logout / lock / password / role-permission / license changes take effect in this
        // service immediately (shared revocation store, checked in OnTokenValidated).
        services.AddTokenRevocation(configuration);

        services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                // RS256 via Identity's JWKS, exact issuer/audience, revocation check — see
                // ErpJwtBearerOptions.
                ErpJwtBearerOptions.Configure(options, configuration);

                // SignalR's browser transport can't set an Authorization header on the
                // WebSocket upgrade request, so its JS client appends "?access_token=" to
                // the connection URL instead (standard SignalR+JWT pattern). JwtBearerHandler
                // only reads the header by default; this reads the query string as a fallback
                // but ONLY under "/hubs" so every other existing authenticated route on every
                // other service keeps working exactly as before.
                //
                // The query value is expected to be a short IHubTicketStore ticket, not the
                // raw JWT — this platform's tokens can carry 240+ permission claims (10KB+),
                // which blows past Kestrel's own default 8KB MaxRequestLineSize before any
                // external proxy even sees the request (a WebSocket handshake 414). If no
                // ticket store is registered (services other than ChatBotService, or the
                // value doesn't resolve to one — e.g. an empty token for a Storefront guest),
                // falls back to using the query value as-is so this stays backward compatible.
                options.Events.OnMessageReceived = context =>
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
                };
            });

        return services;
    }
}
