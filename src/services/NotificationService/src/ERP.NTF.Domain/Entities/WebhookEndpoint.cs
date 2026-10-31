using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities;

public class WebhookEndpoint : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    /// <summary>The signing secret, sealed with ISecretProtector (<c>enc:v1:</c>). Rows written before ROADMAP P2-1b hold the plaintext until they are saved again.</summary>
    public string? Secret { get; private set; }
    public bool IsActive { get; private set; }
    // Comma-separated event type filter (e.g. "notification.sent,notification.failed"). Empty = all events.
    public string EventTypesRaw { get; private set; } = string.Empty;

    public IReadOnlyList<string> EventTypes =>
        EventTypesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim()).ToList().AsReadOnly();

    public bool HasSecret => !string.IsNullOrEmpty(Secret);

    private WebhookEndpoint() { }

    public static WebhookEndpoint Create(string name, string url, string? sealedSecret, IEnumerable<string> eventTypes)
    {
        return new WebhookEndpoint
        {
            Id = Guid.NewGuid(),
            Name = name,
            Url = url,
            Secret = string.IsNullOrEmpty(sealedSecret) ? null : sealedSecret,
            IsActive = true,
            EventTypesRaw = string.Join(",", eventTypes),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Changes name, address and events. The secret is NOT touched: an edit that does not mention it must not wipe it (use <see cref="SetSecret"/> / <see cref="ClearSecret"/>).</summary>
    public void Update(string name, string url, IEnumerable<string> eventTypes)
    {
        Name = name;
        Url = url;
        EventTypesRaw = string.Join(",", eventTypes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSecret(string sealedSecret)
    {
        Secret = sealedSecret;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearSecret()
    {
        Secret = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
}
