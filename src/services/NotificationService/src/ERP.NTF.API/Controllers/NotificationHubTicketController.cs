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
public class NotificationHubTicketController : ControllerBase
{
    private readonly IHubTicketStore _ticketStore;

    public NotificationHubTicketController(IHubTicketStore ticketStore)
    {
        _ticketStore = ticketStore;
    }

    [HttpPost]
    public IActionResult IssueTicket()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { message = "A Bearer token is required to obtain a Hub ticket." });

        var token = authHeader["Bearer ".Length..];
        var ticket = _ticketStore.IssueTicket(token);
        return Ok(new { ticket });
    }
}
