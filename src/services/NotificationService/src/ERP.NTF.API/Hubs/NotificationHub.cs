using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ERP.NTF.API.Hubs;

/// <summary>
/// Unlike ChatBotService's ChatHub, there is no anonymous-guest scenario here — every
/// caller must be an authenticated user, since a bell only ever shows that user's own
/// notifications. Each connection auto-joins its own group on connect (no client-side
/// "join" RPC needed, since there's no per-conversation concept — a user just gets their
/// own notifications by virtue of being themselves).
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));

        await base.OnConnectedAsync();
    }

    internal static string GroupName(string userId) => $"user-{userId}";
}
