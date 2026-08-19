using Asp.Versioning;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

/// <summary>
/// Mirrors ChatBotService's HubTicketController exactly — exchanges the caller's normal
/// (large) bearer token for a short single-use ticket the SignalR client puts on the Hub
/// connection URL instead. See IHubTicketStore and AuthenticationExtensions.
/// AddErpJwtAuthentication's "/hubs" OnMessageReceived fallback for why: this platform's
/// JWTs carry 240+ permission claims (10KB+), which blows past Kestrel's default 8KB
/// request-line limit if put directly on a WebSocket URL.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications/hub-ticket")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class NotificationHubTicketController : ControllerBase
{
    private readonly IHubTicketStore _ticketStore;

    public NotificationHubTicketController(IHubTicketStore ticketStore)
    {
        _ticketStore = ticketStore;
    }

    // [Authorize] above means the standard JWT pipeline (signature/expiry) already ran
    // before this action executes — an expired/invalid token 401s here the normal way,
    // which the frontend's axios interceptor already knows how to catch and retry after
    // a token refresh. Previously this only checked for a "Bearer " prefix and wrapped
    // ANY string (expired or not) into a "successful" ticket, deferring real validation
    // to the SignalR negotiate call downstream — which has no refresh-and-retry path, so
    // an expired token silently broke the bell with no way to recover until a full
    // page reload. The header is still read manually (not via [FromHeader]/model binding)
    // since it's just forwarded as an opaque string into the ticket, never parsed here.
    [HttpPost]
    public IActionResult IssueTicket()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        var token = authHeader["Bearer ".Length..];
        var ticket = _ticketStore.IssueTicket(token);
        return Ok(new { ticket });
    }
}
