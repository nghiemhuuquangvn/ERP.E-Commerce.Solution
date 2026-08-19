using System.Text.Json;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ERP.Identity.Infrastructure.OpenIddict
{
    /// <summary>
    /// Dev/test-only seed: creates the OpenIddictApplications needed to exercise both
    /// the A1 client_credentials flow and the A2 Authorization Code+PKCE flow —
    /// OpenIddict has no admin UI out of the box, and a real client-management API is
    /// a documented follow-up (CommonServiceRoadmap.md §1), not built in this pass.
    /// Client secrets must be created through IOpenIddictApplicationManager (it hashes
    /// them internally), not a raw SQL insert, which is why this is a runtime seeder
    /// rather than a numbered .sql migration like the rest of this service's seed
    /// data. Only invoked from Program.cs when IsDevelopment() — never runs
    /// unattended in Production.
    /// </summary>
    public static class OpenIddictClientSeeder
    {
        public const string DevTestClientId = "dev-test-client";

        /// <summary>Public client (no secret — PKCE is the confidentiality mechanism
        /// for a browser/mobile app that can't keep a secret) for the A2 Authorization
        /// Code flow. Redirect URI is a placeholder that intentionally doesn't resolve
        /// to anything real — verification captures the "code" from the redirect
        /// Location header without following it (see the roadmap doc's A2
        /// verification notes).</summary>
        public const string DevTestSpaClientId = "dev-test-spa";
        public const string DevTestSpaRedirectUri = "https://spa.example.local/callback";

        public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            using var scope = services.CreateScope();
            var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var tenant = await dbContext.Tenants.OrderBy(t => t.CreatedAt).FirstOrDefaultAsync(ct);
            if (tenant is null)
                return;

            if (await applicationManager.FindByClientIdAsync(DevTestClientId, ct) is null)
            {
                await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = DevTestClientId,
                    ClientSecret = "dev-test-secret-CHANGE-ME",
                    DisplayName = "Dev test client (client_credentials) — Part A1 verification seed, not for production use",
                    Permissions =
                    {
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.ClientCredentials
                    },
                    Properties =
                    {
                        ["tenant_id"] = JsonSerializer.SerializeToElement(tenant.Id.ToString()),
                        ["permissions"] = JsonSerializer.SerializeToElement("security:webhooks:view")
                    }
                }, ct);
            }

            if (await applicationManager.FindByClientIdAsync(DevTestSpaClientId, ct) is null)
            {
                await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = DevTestSpaClientId,
                    ClientType = ClientTypes.Public,
                    DisplayName = "Dev test SPA (authorization_code+PKCE) — Part A2 verification seed, not for production use",
                    RedirectUris = { new Uri(DevTestSpaRedirectUri) },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        Permissions.Scopes.Email,
                        Permissions.Scopes.Profile,
                        Permissions.Prefixes.Scope + Scopes.OpenId
                    },
                    Requirements =
                    {
                        Requirements.Features.ProofKeyForCodeExchange
                    }
                }, ct);
            }
        }
    }
}
