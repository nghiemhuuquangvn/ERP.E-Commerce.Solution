using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities;

public class WebhookEndpoint : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string? Secret { get; private set; }
    public bool IsActive { get; private set; }
    // Comma-separated event type filter (e.g. "notification.sent,notification.failed"). Empty = all events.
    public string EventTypesRaw { get; private set; } = string.Empty;

    public IReadOnlyList<string> EventTypes =>
        EventTypesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim()).ToList().AsReadOnly();

    private WebhookEndpoint() { }

    public static WebhookEndpoint Create(string name, string url, string? secret, IEnumerable<string> eventTypes)
    {
        return new WebhookEndpoint
        {
            Id = Guid.NewGuid(),
            Name = name,
            Url = url,
            Secret = secret,
            IsActive = true,
            EventTypesRaw = string.Join(",", eventTypes),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string url, string? secret, IEnumerable<string> eventTypes)
    {
        Name = name;
        Url = url;
        Secret = secret;
        EventTypesRaw = string.Join(",", eventTypes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
}
