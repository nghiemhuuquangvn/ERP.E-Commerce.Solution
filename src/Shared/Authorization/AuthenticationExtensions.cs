using ERP.Shared.Authorization.TokenRevocation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
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
                    var token = HubTokenResolver.Resolve(context.HttpContext);
                    if (token != null)
                        context.Token = token;
                    return Task.CompletedTask;
                };
            });

        return services;
    }
}

/// <summary>
/// The token of a request to a SignalR hub (under /hubs). The JS client sends the negotiate POST with
/// `Authorization: Bearer <value>` and the WebSocket / SSE request with `?access_token=<value>` - the value its
/// accessTokenFactory returned, here a single-use IHubTicketStore ticket. Both places are read, and a ticket is exchanged
/// for the real bearer token; a value that is not a ticket (a real JWT) is used as it is. Without the header branch the
/// negotiate of every hub was refused with 401 and the admin bell / chat never connected. Null = not a hub request or nothing
/// to read: the default header handling applies.
/// </summary>
public static class HubTokenResolver
{
    public static string? Resolve(HttpContext http)
    {
        if (!http.Request.Path.StartsWithSegments("/hubs"))
            return null;

        string? value = http.Request.Query["access_token"];
        if (string.IsNullOrEmpty(value))
        {
            var header = http.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                value = header["Bearer ".Length..].Trim();
        }
        if (string.IsNullOrEmpty(value))
            return null;

        var ticketStore = http.RequestServices.GetService<IHubTicketStore>();
        return ticketStore != null && ticketStore.TryConsumeTicket(value, out var realToken) ? realToken : value;
    }
}
