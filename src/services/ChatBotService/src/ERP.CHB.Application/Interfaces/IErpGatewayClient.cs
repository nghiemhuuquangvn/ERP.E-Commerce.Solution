namespace ERP.CHB.Application.Interfaces;

/// <summary>
/// Thin HTTP helper for tool implementations to call another service's REST API
/// through the Ocelot Gateway. Every call forwards the given bearer token as-is
/// (the caller's own JWT for normal tools; null for guest-callable public endpoints) —
/// see IErpTool's doc comment for why this must never be a service-elevated token.
/// </summary>
public interface IErpGatewayClient
{
    Task<string> GetAsync(string relativePath, string? bearerToken, CancellationToken ct);
    Task<string> PostAsync(string relativePath, object body, string? bearerToken, CancellationToken ct);
}

/// <summary>
/// Opens a CRM case on the caller's behalf using the CALLER'S OWN forwarded JWT (same rule
/// as every IErpTool — see IErpTool's doc comment). This means escalation only succeeds for
/// callers whose account already holds crm:contacts:view + crm:cases:manage (typically
/// staff on the Admin channel, or any role a SuperAdmin has explicitly granted it to) — see
/// CRMService's CasesController.Open. A genuine unprivileged Storefront customer will get a
/// graceful "couldn't open a case automatically" tool result rather than a crash; giving
/// ChatBotService its own elevated service identity that can act across arbitrary tenants
/// would need a new cross-tenant service-principal capability in IdentityService that does
/// not exist today — deliberately out of scope for this MVP rather than faked.
/// </summary>
public interface ICrmEscalationClient
{
    Task<Guid> OpenCaseAsync(Guid callerUserId, string? callerJwt, string subject, string transcriptSummary, CancellationToken ct);
}
