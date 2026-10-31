using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using ERP.Shared.Security;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>
/// Delivers one event to a webhook endpoint. The address is checked again here (it may have been saved before the SSRF rule existed) and, at connect
/// time, against the resolved addresses (the "webhook" HttpClient's ConnectCallback); redirects are not followed. The secret is opened from its
/// sealed form only for the HMAC. Every attempt, successful or not, is written to the delivery log (ROADMAP P2-1b).
/// </summary>
public class WebhookSender : IWebhookSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretProtector _protector;
    private readonly IWebhookRepository _webhooks;
    private readonly ILogger<WebhookSender> _logger;

    public WebhookSender(IHttpClientFactory httpClientFactory, ISecretProtector protector, IWebhookRepository webhooks, ILogger<WebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _protector = protector;
        _webhooks = webhooks;
        _logger = logger;
    }

    public async Task<WebhookSendResult> SendAsync(WebhookEndpoint endpoint, string eventType, object payload, CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        int? status = null;
        string? error = null;

        if (!WebhookUrlRules.IsAllowedUrl(endpoint.Url))
        {
            error = "The address is not allowed: it must be a public https address.";
        }
        else
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url) { Content = content };
                request.Headers.TryAddWithoutValidation("X-Event-Type", eventType);
                request.Headers.TryAddWithoutValidation("X-Timestamp", timestamp);

                if (endpoint.HasSecret)
                {
                    var secret = SecretEnvelope.Open(_protector, endpoint.Secret!);
                    request.Headers.TryAddWithoutValidation("X-Hub-Signature", $"sha256={ComputeHmacSha256($"{timestamp}.{json}", secret)}");
                }

                using var response = await _httpClientFactory.CreateClient("webhook").SendAsync(request, ct);
                status = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode) error = $"The endpoint answered HTTP {status}.";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
            }
        }

        watch.Stop();
        var result = new WebhookSendResult(error == null, status, (int)watch.ElapsedMilliseconds, error);
        if (result.Success) _logger.LogInformation("Webhook delivered event {EventType} to endpoint {Endpoint} (HTTP {Status})", eventType, endpoint.Id, status);
        else _logger.LogWarning("Webhook event {EventType} to endpoint {Endpoint} failed: {Error}", eventType, endpoint.Id, error);

        try
        {
            await _webhooks.AddDeliveryAsync(WebhookDelivery.Create(endpoint.Id, eventType, result.Success, status, error, result.DurationMs), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The delivery of event {EventType} to endpoint {Endpoint} could not be logged", eventType, endpoint.Id);
        }
        return result;
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}
