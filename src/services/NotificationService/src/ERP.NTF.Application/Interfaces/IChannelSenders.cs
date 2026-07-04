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

public interface IWebhookSender
{
    Task SendAsync(string url, string? secret, string eventType, object payload, CancellationToken ct = default);
}
