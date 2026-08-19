using ERP.Identity.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ERP.Identity.Api.Authorization;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>Header a caller presents a raw API key in — see ApiKeyService's
    /// "erpsk_"-prefixed key format.</summary>
    public const string HeaderName = "X-Api-Key";
}

/// <summary>
/// Second authentication scheme alongside the default JWT bearer one — lets a
/// service-to-service caller authenticate with an API key (created via
/// ApiKeysController) instead of a short-lived user JWT. Validates via the existing
/// IApiKeyService.ValidateAsync (already fully implemented, just never wired to
/// anything — see that method's doc comment). On success, builds a ClaimsPrincipal
/// carrying the same claim types [AuthorizePermission]/JwtClaimsUserContextService
/// already read from a JWT (tenant_id, "permission") so existing permission checks
/// work unchanged for an API-key caller. "sub" is set to the API key's own id (not a
/// real user) — ERP.Shared.Authorization.UserContext.IsAuthenticated requires a
/// non-empty UserId (sourced from "sub"), so omitting it entirely (as this handler
/// originally did) made [AuthorizePermission] 401 every API-key call regardless of a
/// valid key/scope match; found while regression-testing Part A1's client_credentials
/// work. No role claims — this scheme is only appropriate for service-to-service
/// calls, not user-self-service endpoints (change-password, etc.) which assume a real
/// caller identity.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationSchemeOptions.HeaderName, out var headerValues))
            return AuthenticateResult.NoResult();

        var rawKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(rawKey))
            return AuthenticateResult.NoResult();

        var apiKey = await _apiKeyService.ValidateAsync(rawKey, requiredScope: null, Context.RequestAborted);
        if (apiKey == null)
            return AuthenticateResult.Fail("Invalid or expired API key.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, apiKey.Id.ToString()),
            new("tenant_id", apiKey.TenantId.ToString()),
            new("api_key_id", apiKey.Id.ToString()),
            new("name", apiKey.Name),
        };

        var scopes = (apiKey.Scopes ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        claims.AddRange(scopes.Select(scope => new Claim("permission", scope)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
