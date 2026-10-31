using ERP.NTF.Application.DTOs;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Security;

namespace ERP.NTF.Application.Webhooks;

/// <summary>The events a shop can subscribe a webhook to (empty list = all of them).</summary>
public static class WebhookEventTypes
{
    public const string NotificationSent = "notification.sent";
    public const string NotificationFailed = "notification.failed";
    /// <summary>Sent only by the "send test" button, to the endpoint being tested; not something to subscribe to.</summary>
    public const string Test = "webhook.test";

    public static readonly IReadOnlyList<WebhookEventTypeDto> Subscribable = new List<WebhookEventTypeDto>
    {
        new() { Type = NotificationSent, Description = "A notification was delivered to its recipient." },
        new() { Type = NotificationFailed, Description = "A notification could not be delivered (it may be retried)." },
    };

    public static bool IsSubscribable(string type) => Subscribable.Any(e => e.Type == type);
}

/// <summary>Validation shared by register and update (BusinessRuleException = HTTP 400).</summary>
public static class WebhookRules
{
    public const int MaxSecretLength = 200;

    public static string Check(string name, string url, string? secret, IEnumerable<string> eventTypes, out List<string> normalizedEvents)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new BusinessRuleException("Webhook.Name", "A webhook needs a name of up to 200 characters.");
        if (!WebhookUrlRules.IsAllowedUrl(url?.Trim()))
            throw new BusinessRuleException("Webhook.Url", "The address must be a public https address (no internal, private or localhost addresses, no credentials in the URL).");
        if (url!.Trim().Length > 2000)
            throw new BusinessRuleException("Webhook.Url", "The address is too long.");
        if (secret is { Length: > MaxSecretLength })
            throw new BusinessRuleException("Webhook.Secret", $"The secret can be at most {MaxSecretLength} characters.");

        normalizedEvents = eventTypes.Select(e => e.Trim()).Where(e => e.Length > 0).Distinct().ToList();
        var unknown = normalizedEvents.FirstOrDefault(e => !WebhookEventTypes.IsSubscribable(e));
        if (unknown != null)
            throw new BusinessRuleException("Webhook.Events", $"Unknown event '{unknown}'.");
        return name.Trim();
    }
}
