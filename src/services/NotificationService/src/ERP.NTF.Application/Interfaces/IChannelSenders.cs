namespace ERP.NTF.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

public interface IPushSender
{
    Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default);
}

/// <summary>What one delivery attempt came to.</summary>
public record WebhookSendResult(bool Success, int? StatusCode, int DurationMs, string? Error);

public interface IWebhookSender
{
    /// <summary>
    /// Delivers one event to the endpoint (address re-checked, secret opened, signed, no redirects) and records the attempt in the delivery log.
    /// Never throws for a failed delivery: the failure is the result.
    /// </summary>
    Task<WebhookSendResult> SendAsync(ERP.NTF.Domain.Entities.WebhookEndpoint endpoint, string eventType, object payload, CancellationToken ct = default);
}
