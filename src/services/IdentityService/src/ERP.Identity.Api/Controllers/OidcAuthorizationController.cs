using System.Net;
using System.Security.Claims;
using ERP.Identity.Api.Authorization;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Security;
using Microsoft.AspNetCore; // OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest lives in this namespace, not OpenIddict.Server.AspNetCore
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ERP.Identity.Api.Controllers;

/// <summary>
/// Interactive OIDC Authorization Code+PKCE flow (Part A2 — CommonServiceRoadmap.md
/// §1) — a new, parallel login surface for third-party apps wanting standards-based
/// redirect login, alongside (not replacing) AuthController's proprietary REST
/// /api/v{version}/auth/login used by this ERP's own frontends.
///
/// Flow: GET/POST /connect/authorize checks for an OidcInteractive cookie session; if
/// absent, redirects to /connect/login (a plain HTML form posting credentials back to
/// itself); once authenticated, resolves which tenant the user is acting as (explicit
/// ?tenant_id=, else a previously-picked cookie claim, else auto-picked if the user
/// belongs to exactly one tenant, else redirected to /connect/tenant to choose) before
/// finally signing the OpenIddict-scheme principal that becomes the issued token.
///
/// Security controls (2026-10-19 hardening): the login form goes through
/// IAuthService.VerifyInteractiveCredentialsAsync (same brute-force counter, per-account
/// lockout, IsActive check and audit as the REST login) under the "auth-sensitive" rate
/// limit; both POSTs require an antiforgery token; every ReturnUrl must be a local
/// /connect/authorize URL (no open redirect); and /connect/authorize re-checks the
/// account via GetInteractiveLoginDenialAsync — a user who requires 2FA is DENIED here
/// (fail-closed) since this flow has no 2FA step yet.
///
/// Remaining documented gap:
/// - Ambiguous email across tenants: login resolves ONLY by password match against
///   every account sharing that email (FindAllByEmailGloballyAsync), taking the
///   first match — mirrors the REST login's own tenant-scoped-uniqueness model but
///   without upfront tenant context. A user with the SAME password on two different
///   tenants' accounts is a genuine edge case left unresolved.
/// </summary>
[Route("connect")]
public class OidcAuthorizationController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly ILicenseService _licenseService;
    private readonly AuthDbContext _dbContext;
    private readonly IAntiforgery _antiforgery;

    public OidcAuthorizationController(
        IUnitOfWork unitOfWork,
        IAuthService authService,
        ILicenseService licenseService,
        AuthDbContext dbContext,
        IAntiforgery antiforgery)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _licenseService = licenseService;
        _dbContext = dbContext;
        _antiforgery = antiforgery;
    }

    [HttpGet("authorize")]
    [HttpPost("authorize")]
    public async Task<IActionResult> Authorize(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var authResult = await HttpContext.AuthenticateAsync(OidcInteractiveAuthenticationDefaults.Scheme);
        var principal = authResult?.Succeeded == true ? authResult.Principal : null;
        var subjectClaim = principal?.FindFirst(Claims.Subject);
        if (principal is null || subjectClaim is null)
            return Redirect($"/connect/login?ReturnUrl={WebUtility.UrlEncode(CurrentAuthorizeUrl())}");

        var userId = Guid.Parse(subjectClaim.Value);
        var user = await _unitOfWork.Users.GetByIdWithDetailsAsync(userId, ct);
        if (user is null)
            return OidcError(Errors.AccessDenied, "The authenticated account no longer exists.");

        var resolution = await ResolveTenantAsync(principal, userId, ct);
        if (resolution.Outcome == TenantResolutionOutcome.NeedsPicker)
            return Redirect($"/connect/tenant?ReturnUrl={WebUtility.UrlEncode(CurrentAuthorizeUrl())}");
        if (resolution.Outcome == TenantResolutionOutcome.Denied)
            return OidcError(Errors.AccessDenied, resolution.Error!);

        var tenant = await _unitOfWork.Tenants.GetByIdAsync(resolution.TenantId!.Value, ct);
        if (tenant is null)
            return OidcError(Errors.AccessDenied, "Tenant not found.");

        // Re-checked on every authorize (not just at login): the cookie session can outlive
        // a deactivation/lock, and 2FA-required accounts must not get a token from this flow.
        var denial = await _authService.GetInteractiveLoginDenialAsync(user.Id, tenant.Id, ct);
        if (denial is not null)
            return OidcError(Errors.AccessDenied, denial);

        // Cache the resolved tenant on the interactive cookie so re-visiting
        // /connect/authorize later this session (e.g. a second client authorizing)
        // doesn't re-prompt the picker.
        if (principal.FindFirst(OidcInteractiveAuthenticationDefaults.SelectedTenantClaim)?.Value != tenant.Id.ToString())
            await ReSignCookieWithTenantAsync(principal, tenant.Id);

        var roles = await _unitOfWork.Roles.GetRolesAsync(user.Id, tenant.Id, ct);
        var permissions = await EffectivePermissions.ResolveAsync(_unitOfWork, user.Id, tenant.Id, roles, ct);
        var licensedModules = await _licenseService.GetEffectiveModulesAsync(tenant.Id, ct);

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(Claims.Email, user.Email);
        identity.SetClaim(Claims.Name, user.FullName ?? user.UserName ?? user.Email);
        identity.SetClaim("tenant_id", tenant.Id.ToString());
        identity.SetClaim("tenant_name", tenant.TenantName);

        foreach (var role in roles)
            identity.AddClaim(new Claim(Claims.Role, role));
        foreach (var permission in permissions)
            identity.AddClaim(new Claim("permission", permission));
        foreach (var module in licensedModules)
            identity.AddClaim(new Claim("licensed_module", module));

        var oidcPrincipal = new ClaimsPrincipal(identity);
        oidcPrincipal.SetScopes(request.GetScopes());
        oidcPrincipal.SetResources("ERP.Services");

        foreach (var claim in oidcPrincipal.Claims)
            claim.SetDestinations(GetDestinations(claim, oidcPrincipal));

        return SignIn(oidcPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("login")]
    public IActionResult Login(string returnUrl, string? error = null)
    {
        if (!IsSafeReturnUrl(returnUrl))
            return BadRequest("Invalid return URL.");

        return Content(RenderLoginPage(returnUrl, error, AntiforgeryField()), "text/html");
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> LoginSubmit([FromForm] string email, [FromForm] string password, [FromForm] string returnUrl, CancellationToken ct)
    {
        if (!await IsAntiforgeryValidAsync())
            return BadRequest("Invalid or missing antiforgery token.");
        if (!IsSafeReturnUrl(returnUrl))
            return BadRequest("Invalid return URL.");

        var userId = await _authService.VerifyInteractiveCredentialsAsync(
            email ?? string.Empty, password ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);

        if (userId is null)
            return Redirect($"/connect/login?ReturnUrl={WebUtility.UrlEncode(returnUrl)}&error={WebUtility.UrlEncode("Invalid email or password.")}");

        var identity = new ClaimsIdentity(OidcInteractiveAuthenticationDefaults.Scheme, Claims.Name, Claims.Role);
        identity.AddClaim(new Claim(Claims.Subject, userId.Value.ToString()));
        identity.AddClaim(new Claim(Claims.Email, email!));

        await HttpContext.SignInAsync(
            OidcInteractiveAuthenticationDefaults.Scheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        return LocalRedirect(returnUrl);
    }

    [HttpGet("tenant")]
    public async Task<IActionResult> TenantPicker(string returnUrl, string? error, CancellationToken ct)
    {
        if (!IsSafeReturnUrl(returnUrl))
            return BadRequest("Invalid return URL.");

        var authResult = await HttpContext.AuthenticateAsync(OidcInteractiveAuthenticationDefaults.Scheme);
        var subjectClaim = authResult?.Succeeded == true ? authResult.Principal.FindFirst(Claims.Subject) : null;
        if (subjectClaim is null)
            return Redirect($"/connect/login?ReturnUrl={WebUtility.UrlEncode(returnUrl)}");

        var userId = Guid.Parse(subjectClaim.Value);
        var memberships = await _dbContext.Set<AuthUserTenant>()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Tenant.TenantName)
            .ToListAsync(ct);

        return Content(RenderTenantPickerPage(returnUrl, memberships, error, AntiforgeryField()), "text/html");
    }

    [HttpPost("tenant")]
    public async Task<IActionResult> TenantPickerSubmit([FromForm] string tenantId, [FromForm] string returnUrl, CancellationToken ct)
    {
        if (!await IsAntiforgeryValidAsync())
            return BadRequest("Invalid or missing antiforgery token.");
        if (!IsSafeReturnUrl(returnUrl))
            return BadRequest("Invalid return URL.");

        var authResult = await HttpContext.AuthenticateAsync(OidcInteractiveAuthenticationDefaults.Scheme);
        var principal = authResult?.Succeeded == true ? authResult.Principal : null;
        var subjectClaim = principal?.FindFirst(Claims.Subject);
        if (principal is null || subjectClaim is null)
            return Redirect($"/connect/login?ReturnUrl={WebUtility.UrlEncode(returnUrl)}");

        var userId = Guid.Parse(subjectClaim.Value);
        if (!Guid.TryParse(tenantId, out var parsedTenantId) || !await _unitOfWork.Users.IsInTenantAsync(userId, parsedTenantId, ct))
            return Redirect($"/connect/tenant?ReturnUrl={WebUtility.UrlEncode(returnUrl)}&error={WebUtility.UrlEncode("Invalid selection.")}");

        await ReSignCookieWithTenantAsync(principal, parsedTenantId);
        return LocalRedirect(returnUrl);
    }

    /// <summary>
    /// The only legitimate ReturnUrl in this flow is the /connect/authorize request that
    /// started it (see CurrentAuthorizeUrl) — anything else (absolute URLs, //host,
    /// other local paths) is rejected so these endpoints can't be used as an open redirect.
    /// </summary>
    private bool IsSafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && Url.IsLocalUrl(returnUrl)
        && returnUrl.StartsWith($"{Request.PathBase}/connect/authorize", StringComparison.OrdinalIgnoreCase);

    private string AntiforgeryField()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return $"<input type=\"hidden\" name=\"{WebUtility.HtmlEncode(tokens.FormFieldName)}\" value=\"{WebUtility.HtmlEncode(tokens.RequestToken)}\" />";
    }

    private async Task<bool> IsAntiforgeryValidAsync()
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private async Task ReSignCookieWithTenantAsync(ClaimsPrincipal principal, Guid tenantId)
    {
        var identity = (ClaimsIdentity)((ClaimsIdentity)principal.Identity!).Clone();
        var existing = identity.FindFirst(OidcInteractiveAuthenticationDefaults.SelectedTenantClaim);
        if (existing is not null)
            identity.RemoveClaim(existing);
        identity.AddClaim(new Claim(OidcInteractiveAuthenticationDefaults.SelectedTenantClaim, tenantId.ToString()));

        await HttpContext.SignInAsync(OidcInteractiveAuthenticationDefaults.Scheme, new ClaimsPrincipal(identity));
    }

    private enum TenantResolutionOutcome { Resolved, NeedsPicker, Denied }

    private sealed record TenantResolutionResult(TenantResolutionOutcome Outcome, Guid? TenantId, string? Error)
    {
        public static TenantResolutionResult Resolved(Guid tenantId) => new(TenantResolutionOutcome.Resolved, tenantId, null);
        public static readonly TenantResolutionResult NeedsPicker = new(TenantResolutionOutcome.NeedsPicker, null, null);
        public static TenantResolutionResult Denied(string error) => new(TenantResolutionOutcome.Denied, null, error);
    }

    private async Task<TenantResolutionResult> ResolveTenantAsync(ClaimsPrincipal principal, Guid userId, CancellationToken ct)
    {

        var requestedTenantId = Request.Query["tenant_id"].ToString();
        if (!string.IsNullOrWhiteSpace(requestedTenantId))
        {
            if (!Guid.TryParse(requestedTenantId, out var parsed) || !await _unitOfWork.Users.IsInTenantAsync(userId, parsed, ct))
                return TenantResolutionResult.Denied("The user is not a member of the requested tenant.");
            return TenantResolutionResult.Resolved(parsed);
        }

        var cachedClaim = principal.FindFirst(OidcInteractiveAuthenticationDefaults.SelectedTenantClaim)?.Value;
        if (!string.IsNullOrWhiteSpace(cachedClaim) && Guid.TryParse(cachedClaim, out var cached)
            && await _unitOfWork.Users.IsInTenantAsync(userId, cached, ct))
        {
            return TenantResolutionResult.Resolved(cached);
        }

        var memberships = await _dbContext.Set<AuthUserTenant>()
            .Where(m => m.UserId == userId)
            .Select(m => m.TenantId)
            .ToListAsync(ct);

        if (memberships.Count == 0)
            return TenantResolutionResult.Denied("The user does not belong to any tenant.");
        if (memberships.Count == 1)
            return TenantResolutionResult.Resolved(memberships[0]);

        return TenantResolutionResult.NeedsPicker;
    }

    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        // Same claims land in both tokens: access token for parity with the
        // proprietary REST-issued JWT (tenant_id/permission/role/licensed_module, so
        // [AuthorizePermission] works unchanged), ID token because surfacing "who
        // logged in" without a separate /connect/userinfo round trip (not built until
        // Part A3) is the point of requesting the openid scope at all.
        yield return Destinations.AccessToken;
        if (principal.HasScope(Scopes.OpenId))
            yield return Destinations.IdentityToken;
    }

    private string CurrentAuthorizeUrl() => Request.PathBase + Request.Path + Request.QueryString;

    private IActionResult OidcError(string error, string description)
    {
        var properties = new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        });
        return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static string RenderLoginPage(string returnUrl, string? error, string antiforgeryField)
    {
        var errorHtml = string.IsNullOrEmpty(error)
            ? string.Empty
            : $"<p style=\"color:#b00020\">{WebUtility.HtmlEncode(error)}</p>";

        return $$"""
            <!doctype html>
            <html><head><meta charset="utf-8"><title>Sign in</title></head>
            <body style="font-family:sans-serif;max-width:360px;margin:80px auto">
              <h2>Sign in</h2>
              {{errorHtml}}
              <form method="post" action="/connect/login">
                {{antiforgeryField}}
                <input type="hidden" name="returnUrl" value="{{WebUtility.HtmlEncode(returnUrl)}}" />
                <p><label>Email<br/><input type="email" name="email" required autofocus /></label></p>
                <p><label>Password<br/><input type="password" name="password" required /></label></p>
                <button type="submit">Sign in</button>
              </form>
            </body></html>
            """;
    }

    private static string RenderTenantPickerPage(string returnUrl, IReadOnlyList<AuthUserTenant> memberships, string? error, string antiforgeryField)
    {
        var errorHtml = string.IsNullOrEmpty(error)
            ? string.Empty
            : $"<p style=\"color:#b00020\">{WebUtility.HtmlEncode(error)}</p>";

        var options = string.Join("\n", memberships.Select(m =>
            $"<option value=\"{m.TenantId}\">{WebUtility.HtmlEncode(m.Tenant.TenantName)}</option>"));

        return $$"""
            <!doctype html>
            <html><head><meta charset="utf-8"><title>Choose account</title></head>
            <body style="font-family:sans-serif;max-width:360px;margin:80px auto">
              <h2>Choose which account to continue as</h2>
              {{errorHtml}}
              <form method="post" action="/connect/tenant">
                {{antiforgeryField}}
                <input type="hidden" name="returnUrl" value="{{WebUtility.HtmlEncode(returnUrl)}}" />
                <p><select name="tenantId" required>{{options}}</select></p>
                <button type="submit">Continue</button>
              </form>
            </body></html>
            """;
    }
}
