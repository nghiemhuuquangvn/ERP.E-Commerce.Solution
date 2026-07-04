using ERP.NTF.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ERP.NTF.Infrastructure.Services;

public class WebhookSender : IWebhookSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookSender> _logger;

    public WebhookSender(IHttpClientFactory httpClientFactory, ILogger<WebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(string url, string? secret, string eventType, object payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var client = _httpClientFactory.CreateClient("webhook");
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.TryAddWithoutValidation("X-Event-Type", eventType);
        request.Headers.TryAddWithoutValidation("X-Timestamp", timestamp);

        if (!string.IsNullOrEmpty(secret))
        {
            var signature = ComputeHmacSha256($"{timestamp}.{json}", secret);
            request.Headers.TryAddWithoutValidation("X-Hub-Signature", $"sha256={signature}");
        }

        try
        {
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Webhook {Url} returned HTTP {Status}", url, (int)response.StatusCode);
            else
                _logger.LogInformation("Webhook delivered event {EventType} to {Url}", eventType, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver webhook event {EventType} to {Url}", eventType, url);
        }
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLowerInvariant();
    }
}
