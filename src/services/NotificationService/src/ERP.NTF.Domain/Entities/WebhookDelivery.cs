using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities;

/// <summary>
/// One attempt to deliver an event to a webhook endpoint (ROADMAP P2-1b): what was sent, what came back and how long it took. Written for every
/// attempt, including the "send test" button and a refused address, so a shop can see why its endpoint is silent.
/// </summary>
public class WebhookDelivery : BaseEntity
{
    public Guid WebhookEndpointId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public bool Success { get; private set; }
    public int? StatusCode { get; private set; }
    public string? Error { get; private set; }
    public int DurationMs { get; private set; }

    private WebhookDelivery() { }

    public static WebhookDelivery Create(Guid endpointId, string eventType, bool success, int? statusCode, string? error, int durationMs) => new()
    {
        Id = Guid.NewGuid(),
        WebhookEndpointId = endpointId,
        EventType = eventType,
        Success = success,
        StatusCode = statusCode,
        Error = error is { Length: > 500 } ? error[..500] : error,
        DurationMs = Math.Max(0, durationMs),
        CreatedAt = DateTime.UtcNow
    };
}
