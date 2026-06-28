using ERP.NTF.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>
/// Firebase Cloud Messaging (FCM) push notification sender.
/// Requires config: Fcm:ServerKey, Fcm:SenderId
/// Supports both Android and iOS via FCM v1 API.
/// </summary>
public class FcmPushSender : IPushSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<FcmPushSender> _logger;
    private readonly HttpClient _http;

    private const string FcmEndpoint = "https://fcm.googleapis.com/fcm/send";

    public FcmPushSender(IConfiguration config, ILogger<FcmPushSender> logger, IHttpClientFactory factory)
    {
        _config = config;
        _logger = logger;
        _http = factory.CreateClient("FCM");
    }

    public async Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default)
    {
        var serverKey = _config["Fcm:ServerKey"];

        if (string.IsNullOrEmpty(serverKey))
        {
            _logger.LogWarning("[PUSH] FCM server key not configured. Skipping push to {Token}", deviceToken[..8] + "...");
            return;
        }

        var payload = new
        {
            to = deviceToken,
            notification = new { title, body },
            priority = "high"
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, FcmEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
            Headers = { Authorization = new AuthenticationHeaderValue("key", $"={serverKey}") }
        };

        var response = await _http.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
            _logger.LogInformation("[PUSH] FCM notification sent: {Title}", title);
        else
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[PUSH] FCM error {Status}: {Error}", response.StatusCode, error);
        }
    }
}
