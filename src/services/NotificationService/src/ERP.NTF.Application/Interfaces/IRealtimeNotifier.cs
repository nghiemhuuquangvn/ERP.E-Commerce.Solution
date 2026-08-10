using ERP.NTF.Application.DTOs;

namespace ERP.NTF.Application.Interfaces;

/// <summary>
/// Pushes an already-persisted InApp notification to the recipient's live bell over
/// SignalR. Kept in Application (mirroring IChatStreamNotifier in ChatBotService) so
/// SendNotificationCommandHandler doesn't depend on ASP.NET Core SignalR types directly —
/// the real implementation (SignalRRealtimeNotifier) lives in the API layer.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotifyAsync(string recipientId, NotificationDto notification, CancellationToken ct = default);
}
