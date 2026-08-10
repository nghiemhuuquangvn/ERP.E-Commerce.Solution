using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ERP.NTF.API.Hubs;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRRealtimeNotifier(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyAsync(string recipientId, NotificationDto notification, CancellationToken ct = default) =>
        _hub.Clients.Group(NotificationHub.GroupName(recipientId)).SendAsync("notificationReceived", notification, ct);
}
