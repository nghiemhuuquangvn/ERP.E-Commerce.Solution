namespace ERP.CHB.Application.Interfaces;

/// <summary>
/// Push channel back to the caller's browser/app. Implemented in the API layer
/// (SignalRChatStreamNotifier, using IHubContext&lt;ChatHub&gt;) — kept as an Application-layer
/// interface so the command handler that owns the tool-calling loop doesn't need to
/// reference ASP.NET Core SignalR types directly.
/// </summary>
public interface IChatStreamNotifier
{
    Task NotifyTextDeltaAsync(Guid conversationId, string delta, CancellationToken ct);
    Task NotifyToolCallStartedAsync(Guid conversationId, string toolName, CancellationToken ct);
    Task NotifyToolCallCompletedAsync(Guid conversationId, string toolName, CancellationToken ct);
    Task NotifyCompletedAsync(Guid conversationId, CancellationToken ct);
    Task NotifyErrorAsync(Guid conversationId, string message, CancellationToken ct);
}
