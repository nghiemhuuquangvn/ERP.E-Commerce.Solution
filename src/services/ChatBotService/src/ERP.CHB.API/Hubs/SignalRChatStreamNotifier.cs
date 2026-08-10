using ERP.CHB.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ERP.CHB.API.Hubs;

/// <summary>
/// Only place in the codebase that touches SignalR types outside ChatHub itself — the
/// Application-layer command handler that owns the tool-calling loop depends on
/// IChatStreamNotifier, not this, so Application stays free of ASP.NET Core references.
/// </summary>
public class SignalRChatStreamNotifier : IChatStreamNotifier
{
    private readonly IHubContext<ChatHub> _hub;

    public SignalRChatStreamNotifier(IHubContext<ChatHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyTextDeltaAsync(Guid conversationId, string delta, CancellationToken ct) =>
        _hub.Clients.Group(ChatHub.GroupName(conversationId)).SendAsync("textDelta", delta, ct);

    public Task NotifyToolCallStartedAsync(Guid conversationId, string toolName, CancellationToken ct) =>
        _hub.Clients.Group(ChatHub.GroupName(conversationId)).SendAsync("toolCallStarted", toolName, ct);

    public Task NotifyToolCallCompletedAsync(Guid conversationId, string toolName, CancellationToken ct) =>
        _hub.Clients.Group(ChatHub.GroupName(conversationId)).SendAsync("toolCallCompleted", toolName, ct);

    public Task NotifyCompletedAsync(Guid conversationId, CancellationToken ct) =>
        _hub.Clients.Group(ChatHub.GroupName(conversationId)).SendAsync("completed", ct);

    public Task NotifyErrorAsync(Guid conversationId, string message, CancellationToken ct) =>
        _hub.Clients.Group(ChatHub.GroupName(conversationId)).SendAsync("error", message, ct);
}
