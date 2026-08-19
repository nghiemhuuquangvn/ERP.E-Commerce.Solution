using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace ERP.Identity.Infrastructure.OpenIddict
{
    /// <summary>
    /// Fills in the ClaimsPrincipal for a client_credentials token request. Mirrors
    /// ApiKeyAuthenticationHandler's claim shape (tenant_id + one "permission" claim
    /// per granted scope) exactly, so [AuthorizePermission]/JwtClaimsUserContextService
    /// keep working unchanged regardless of whether the caller authenticated via
    /// X-Api-Key or an OpenIddict-issued token. tenant_id/permission list are read from
    /// the OpenIddictApplication's custom Properties (there's no dedicated column for
    /// them — OpenIddict's own "scope"/"permission" concepts are about which
    /// OAuth2 flows/endpoints a client may use, not this platform's RBAC permission
    /// codes, so they're kept separate). See CommonServiceRoadmap.md §1 / Part A1.
    /// </summary>
    public sealed class ClientCredentialsClaimsHandler : IOpenIddictServerHandler<HandleTokenRequestContext>
    {
        private readonly IOpenIddictApplicationManager _applicationManager;

        public ClientCredentialsClaimsHandler(IOpenIddictApplicationManager applicationManager)
        {
            _applicationManager = applicationManager;
        }

        // Scoped, not singleton: this handler depends on IOpenIddictApplicationManager,
        // which is itself scoped (backed by a per-request AuthDbContext).
        public static OpenIddictServerHandlerDescriptor Descriptor { get; }
            = OpenIddictServerHandlerDescriptor.CreateBuilder<HandleTokenRequestContext>()
                .UseScopedHandler<ClientCredentialsClaimsHandler>()
                .SetOrder(int.MinValue + 100_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        public async ValueTask HandleAsync(HandleTokenRequestContext context)
        {
            if (!context.Request.IsClientCredentialsGrantType())
                return;

            var application = await _applicationManager.FindByClientIdAsync(context.ClientId!)
                ?? throw new InvalidOperationException("The application details cannot be found.");

            var identity = new ClaimsIdentity(
                authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                nameType: Claims.Name,
                roleType: Claims.Role);

            identity.SetClaim(Claims.Subject, await _applicationManager.GetClientIdAsync(application));
            identity.SetClaim(Claims.Name, await _applicationManager.GetDisplayNameAsync(application));

            var properties = await _applicationManager.GetPropertiesAsync(application);

            if (properties.TryGetValue("tenant_id", out var tenantIdElement))
                identity.SetClaim("tenant_id", tenantIdElement.GetString());

            if (properties.TryGetValue("permissions", out var permissionsElement))
            {
                var permissions = (permissionsElement.GetString() ?? string.Empty)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var permission in permissions)
                    identity.AddClaim(new Claim("permission", permission));
            }

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(context.Request.GetScopes());

            // Fixed resource/audience matching TokenService's existing Jwt:Audience value
            // (see Program.cs) so a client_credentials token validates against the same
            // "Bearer" JwtBearer scheme the proprietary /api/auth/login flow already uses
            // — no second validation scheme needed for Phase A1.
            principal.SetResources("ERP.Services");

            foreach (var claim in principal.Claims)
                claim.SetDestinations(Destinations.AccessToken);

            context.Principal = principal;
        }
    }
}
