using Asp.Versioning;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.CHB.API.Controllers;

/// <summary>
/// Exchanges the caller's normal (large) bearer token for a short single-use ticket the
/// SignalR client puts on the Hub connection URL instead — see IHubTicketStore and
/// AuthenticationExtensions.AddErpJwtAuthentication's "/hubs" OnMessageReceived fallback
/// for why. Requires a real Authorization header the normal way (a REST call, no query-
/// string size problem here) — there is nothing for an unauthenticated Storefront guest
/// to exchange, so guests skip this entirely and connect with an empty access_token.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chatbot/hub-ticket")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class HubTicketController : ControllerBase
{
    private readonly IHubTicketStore _ticketStore;

    public HubTicketController(IHubTicketStore ticketStore)
    {
        _ticketStore = ticketStore;
    }

    // [Authorize] above means an expired/invalid token 401s here via the standard JWT
    // pipeline, the normal way the frontend's axios interceptor already knows how to
    // catch and retry after a token refresh — see NotificationHubTicketController's
    // matching fix/comment for the full "silently wrapped an expired token, only failed
    // much later at negotiate with no recovery path" bug this closes.
    [HttpPost]
    public IActionResult IssueTicket()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        var token = authHeader["Bearer ".Length..];
        var ticket = _ticketStore.IssueTicket(token);
        return Ok(new { ticket });
    }
}
