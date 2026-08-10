using ERP.CHB.Application.Commands;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ERP.CHB.API.Hubs;

/// <summary>
/// [AllowAnonymous] at the hub level because Storefront guests must be able to connect —
/// StartConversationCommandHandler is what actually enforces "guest channel must be
/// Storefront" and blocks premium provider access for unauthenticated callers. Groups are
/// keyed "conversation-{id}" so SignalRChatStreamNotifier can push without tracking
/// connection ids itself.
/// </summary>
[AllowAnonymous]
public class ChatHub : Hub
{
    private readonly IMediator _mediator;

    public ChatHub(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task JoinConversation(Guid conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    public async Task SendMessage(Guid conversationId, string content)
    {
        var caller = TryGetUserContext();
        var callerJwt = TryGetBearerToken();
        await _mediator.Send(new SendMessageCommand(conversationId, content, caller, callerJwt));
    }

    /// <summary>
    /// UserContext is normally populated per-HTTP-request by UserContextHeaderMiddleware,
    /// which never runs for a SignalR invocation (it rides the initial negotiate/connect
    /// request, not each Hub method call) — so the Hub reads straight off
    /// Context.User (the JWT ClaimsPrincipal ASP.NET Core authentication already
    /// populated from the access_token query param, see AddErpJwtAuthentication's
    /// OnMessageReceived in Program.cs) instead of depending on that middleware.
    /// </summary>
    private UserContext? TryGetUserContext()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
            return null;

        var principal = Context.User;
        return new UserContext
        {
            UserId = principal.FindFirst("sub")?.Value ?? string.Empty,
            UserName = principal.FindFirst("name")?.Value ?? string.Empty,
            TenantId = principal.FindFirst("tenant_id")?.Value ?? string.Empty,
            Roles = principal.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray(),
            Permissions = principal.FindAll("permission").Select(c => c.Value).ToArray(),
            LicensedModules = principal.FindAll("licensed_module").Select(c => c.Value).ToArray(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
    }

    private string? TryGetBearerToken() =>
        Context.GetHttpContext()?.Request.Query["access_token"].FirstOrDefault();

    internal static string GroupName(Guid conversationId) => $"conversation-{conversationId}";
}
