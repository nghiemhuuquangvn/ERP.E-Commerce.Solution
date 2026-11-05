using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ERP.NTF.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>Firebase settings (<c>Fcm:*</c>). Push is switched on only when both are set; until the owner provides a Firebase project nothing is sent.</summary>
public class FcmOptions
{
    public string ProjectId { get; set; } = string.Empty;
    /// <summary>Path of the service-account JSON (kept as a secret, mounted into the container).</summary>
    public string ServiceAccountPath { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ProjectId) && !string.IsNullOrWhiteSpace(ServiceAccountPath);
}

/// <summary>The bearer token for the FCM HTTP v1 API (OAuth2, scope firebase.messaging).</summary>
public interface IFcmAccessTokenProvider
{
    Task<string> GetAsync(CancellationToken ct);
    /// <summary>Forgets the cached token (FCM answered 401 to it).</summary>
    void Invalidate();
}

/// <summary>
/// Gets an access token from a Google service-account JSON without any extra package: a short RS256-signed JWT is exchanged at the token endpoint,
/// the answer is cached until a minute before it expires.
/// </summary>
public sealed class FcmServiceAccountTokenProvider : IFcmAccessTokenProvider
{
    public const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly Func<string> _readJson;
    private readonly HttpClient _http;
    private readonly Func<DateTime> _now;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTime _expires;

    public FcmServiceAccountTokenProvider(Func<string> readJson, HttpClient http, Func<DateTime>? now = null)
    {
        _readJson = readJson;
        _http = http;
        _now = now ?? (() => DateTime.UtcNow);
    }

    public void Invalidate() => _token = null;

    public async Task<string> GetAsync(CancellationToken ct)
    {
        if (_token != null && _now() < _expires) return _token;
        await _gate.WaitAsync(ct);
        try
        {
            if (_token != null && _now() < _expires) return _token;

            using var account = JsonDocument.Parse(_readJson());
            var root = account.RootElement;
            var email = root.GetProperty("client_email").GetString() ?? throw new ERP.Shared.Infrastructure.Exceptions.ServiceMisconfiguredException("The service account has no client_email.");
            var key = root.GetProperty("private_key").GetString() ?? throw new ERP.Shared.Infrastructure.Exceptions.ServiceMisconfiguredException("The service account has no private_key.");
            var tokenUri = root.TryGetProperty("token_uri", out var uri) ? uri.GetString() ?? "https://oauth2.googleapis.com/token" : "https://oauth2.googleapis.com/token";

            var issued = new DateTimeOffset(_now()).ToUnixTimeSeconds();
            var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
            var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = email, scope = Scope, aud = tokenUri, iat = issued, exp = issued + 3600 }));
            using var rsa = RSA.Create();
            rsa.ImportFromPem(key);
            var signature = Base64Url(rsa.SignData(Encoding.UTF8.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            using var response = await _http.PostAsync(tokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = $"{header}.{claims}.{signature}",
            }), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"The Google token endpoint answered {(int)response.StatusCode}.");

            using var answer = JsonDocument.Parse(body);
            _token = answer.RootElement.GetProperty("access_token").GetString()!;
            var seconds = answer.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            _expires = _now().AddSeconds(Math.Max(60, seconds) - 60);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Push through the Firebase Cloud Messaging HTTP v1 API (the legacy /fcm/send endpoint was shut down by Google). One message per device token;
/// a token FCM no longer knows (UNREGISTERED / NOT_FOUND / an invalid token) is reported as <see cref="InvalidPushTokenException"/> so it is forgotten.
/// </summary>
public class FcmV1PushSender : IPushSender
{
    public const string DefaultBaseUrl = "https://fcm.googleapis.com";
    private readonly HttpClient _http;
    private readonly IFcmAccessTokenProvider _tokens;
    private readonly FcmOptions _options;
    private readonly ILogger<FcmV1PushSender> _logger;

    public FcmV1PushSender(HttpClient http, IFcmAccessTokenProvider tokens, FcmOptions options, ILogger<FcmV1PushSender> logger)
    {
        _http = http;
        _tokens = tokens;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default, IReadOnlyDictionary<string, string>? data = null)
    {
        var message = new Dictionary<string, object?>
        {
            ["token"] = deviceToken,
            ["notification"] = new { title, body },
            ["android"] = new { priority = "HIGH" },
        };
        if (data is { Count: > 0 }) message["data"] = data;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{(_http.BaseAddress?.ToString().TrimEnd('/') ?? DefaultBaseUrl)}/v1/projects/{_options.ProjectId}/messages:send")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { message }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetAsync(ct));

        using var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return;

        var text = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _tokens.Invalidate();
            throw new HttpRequestException("FCM refused the access token (401); a new one is fetched on the next try.");
        }
        if (response.StatusCode == HttpStatusCode.NotFound || text.Contains("UNREGISTERED", StringComparison.Ordinal))
            throw new InvalidPushTokenException("FCM does not know this device token any more.");
        if (response.StatusCode == HttpStatusCode.BadRequest && text.Contains("INVALID_ARGUMENT", StringComparison.Ordinal) && text.Contains("token", StringComparison.OrdinalIgnoreCase))
            throw new InvalidPushTokenException("FCM refused this device token as invalid.");

        _logger.LogWarning("FCM answered {Status}: {Body}", (int)response.StatusCode, text.Length > 300 ? text[..300] : text);
        throw new HttpRequestException($"FCM answered {(int)response.StatusCode}.");
    }
}

/// <summary>Push is real only when Firebase is configured.</summary>
public class PushStatus : IPushStatus
{
    public PushStatus(bool isEnabled) => IsEnabled = isEnabled;
    public bool IsEnabled { get; }
}
