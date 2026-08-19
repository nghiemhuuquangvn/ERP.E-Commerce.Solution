using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace ERP.Identity.Infrastructure.OpenIddict
{
    /// <summary>
    /// Populates /connect/userinfo's response from the presented access token's own
    /// claims (Part A3 — CommonServiceRoadmap.md §1). OpenIddict's built-in userinfo
    /// handling only forwards a small set of OIDC-standard claims by default; this
    /// adds the platform-specific ones (tenant_id/tenant_name/permission/role/
    /// licensed_module) so a client that only has an access token (not the ID token)
    /// can still discover the same claim set via this endpoint, matching what
    /// ClientCredentialsClaimsHandler (A1) / OidcAuthorizationController (A2) already
    /// put on the token itself.
    /// </summary>
    public sealed class UserinfoClaimsHandler : IOpenIddictServerHandler<HandleUserInfoRequestContext>
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; }
            = OpenIddictServerHandlerDescriptor.CreateBuilder<HandleUserInfoRequestContext>()
                .UseSingletonHandler<UserinfoClaimsHandler>()
                .SetOrder(int.MinValue + 100_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        public ValueTask HandleAsync(HandleUserInfoRequestContext context)
        {
            var principal = context.AccessTokenPrincipal;
            if (principal is null)
                return default;

            context.Subject = principal.GetClaim(Claims.Subject);

            var email = principal.GetClaim(Claims.Email);
            if (!string.IsNullOrEmpty(email))
                context.Email = email;

            var name = principal.GetClaim(Claims.Name);
            if (!string.IsNullOrEmpty(name))
                context.Claims[Claims.Name] = name;

            var tenantId = principal.GetClaim("tenant_id");
            if (!string.IsNullOrEmpty(tenantId))
                context.Claims["tenant_id"] = tenantId;

            var tenantName = principal.GetClaim("tenant_name");
            if (!string.IsNullOrEmpty(tenantName))
                context.Claims["tenant_name"] = tenantName;

            var roles = principal.GetClaims(Claims.Role);
            if (roles.Length > 0)
                context.Claims[Claims.Role] = new OpenIddictParameter(roles.As<string?>());

            var permissions = principal.GetClaims("permission");
            if (permissions.Length > 0)
                context.Claims["permission"] = new OpenIddictParameter(permissions.As<string?>());

            var licensedModules = principal.GetClaims("licensed_module");
            if (licensedModules.Length > 0)
                context.Claims["licensed_module"] = new OpenIddictParameter(licensedModules.As<string?>());

            return default;
        }
    }
}
