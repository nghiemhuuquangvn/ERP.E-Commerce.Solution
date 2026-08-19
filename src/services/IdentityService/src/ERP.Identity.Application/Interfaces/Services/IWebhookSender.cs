namespace ERP.Identity.Application.Interfaces.Services
{
    /// <summary>
    /// Fire-and-forget HTTP delivery for a webhook event — mirrors NotificationService's
    /// IWebhookSender/WebhookSender exactly (same signing scheme) for consistency across
    /// services. No retry/dead-letter queue, matching that existing precedent: a failed
    /// delivery is logged and dropped, not persisted for retry.
    /// </summary>
    public interface IWebhookSender
    {
        Task SendAsync(string url, string? secret, string eventType, object payload, CancellationToken ct = default);
    }
}
