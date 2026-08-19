namespace ERP.Identity.Api.Authorization;

/// <summary>
/// A cookie authentication scheme used ONLY by the interactive OIDC Authorization
/// Code+PKCE flow (Part A2 — see CommonServiceRoadmap.md §1) to track a logged-in
/// browser session across the /connect/authorize -> /connect/login ->
/// /connect/authorize -> [/connect/tenant -> /connect/authorize] redirect chain. Not
/// the default scheme — "SmartAuth" (JWT/API-key) stays default for every existing
/// API endpoint; this scheme is only ever referenced explicitly by name from
/// OidcAuthorizationController.
/// </summary>
public static class OidcInteractiveAuthenticationDefaults
{
    public const string Scheme = "OidcInteractive";

    /// <summary>Claim on the interactive cookie identity recording which tenant the
    /// user picked this session, once resolved (home tenant auto-selected, or chosen
    /// via the tenant-picker) — read back on the next /connect/authorize pass so the
    /// picker doesn't re-prompt every time.</summary>
    public const string SelectedTenantClaim = "selected_tenant_id";
}
