using ERP.NTF.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>
/// Twilio SMS sender. Requires config: Twilio:AccountSid, Twilio:AuthToken, Twilio:FromNumber
/// Falls back to NullSmsSender if credentials are absent (dev/test).
/// </summary>
public class TwilioSmsSender : ISmsSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<TwilioSmsSender> _logger;
    private readonly HttpClient _http;

    public TwilioSmsSender(IConfiguration config, ILogger<TwilioSmsSender> logger, IHttpClientFactory factory)
    {
        _config = config;
        _logger = logger;
        _http = factory.CreateClient("Twilio");
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var accountSid = _config["Twilio:AccountSid"];
        var authToken = _config["Twilio:AuthToken"];
        var fromNumber = _config["Twilio:FromNumber"];

        if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
        {
            _logger.LogWarning("[SMS] Twilio credentials not configured. Skipping SMS to {Phone}", phoneNumber);
            return;
        }

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));

        var formData = new Dictionary<string, string>
        {
            ["To"] = phoneNumber,
            ["From"] = fromNumber ?? string.Empty,
            ["Body"] = message
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(formData),
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) }
        };

        var response = await _http.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
            _logger.LogInformation("[SMS] Sent to {Phone} via Twilio", phoneNumber);
        else
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[SMS] Twilio error {Status}: {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"Twilio SMS failed: {response.StatusCode}");
        }
    }
}
