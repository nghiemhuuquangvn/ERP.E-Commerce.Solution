using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ERP.SHP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Infrastructure.Carriers;

/// <summary>Shared plumbing of the carrier providers: hosts from configuration, JSON calls that never throw, shared-secret check.</summary>
public abstract class CarrierProviderBase
{
    protected readonly IConfiguration Config;
    protected readonly ILogger Log;
    private readonly IHttpClientFactory _http;

    protected CarrierProviderBase(IConfiguration config, ILogger log, IHttpClientFactory http)
    {
        Config = config;
        Log = log;
        _http = http;
    }

    /// <summary>The carrier's host for the account's mode. Configuration only: a tenant can never point a call at an address of its choosing.</summary>
    protected string? BaseUrl(string configSection, bool sandbox, string sandboxDefault, string? productionDefault) =>
        (sandbox ? Config[$"{configSection}:SandboxBaseUrl"] ?? sandboxDefault : Config[$"{configSection}:BaseUrl"] ?? productionDefault)?.TrimEnd('/');

    protected static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };

    /// <summary>Sends one request. Network failures and timeouts become a result with an error text; secrets are never logged.</summary>
    protected async Task<(bool Reached, int Status, string Body, string? Error)> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var client = _http.CreateClient("Carriers");
            client.Timeout = TimeSpan.FromSeconds(20);
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return (true, (int)response.StatusCode, body, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            Log.LogWarning("Carrier call {Method} {Path} failed: {Message}", request.Method, request.RequestUri?.AbsolutePath, ex.Message);
            return (false, 0, string.Empty, "the carrier is not reachable: " + ex.Message);
        }
    }

    protected static StringContent Json(object body) => new(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

    protected static JsonElement? Parse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    protected static string? Str(JsonElement? e, params string[] path)
    {
        var current = e;
        foreach (var name in path)
        {
            if (current is not { ValueKind: JsonValueKind.Object } obj || !obj.TryGetProperty(name, out var next)) return null;
            current = next;
        }
        return current switch
        {
            { ValueKind: JsonValueKind.String } s => s.GetString(),
            { ValueKind: JsonValueKind.Number } n => n.GetRawText(),
            { ValueKind: JsonValueKind.True } => "true",
            { ValueKind: JsonValueKind.False } => "false",
            _ => null
        };
    }

    protected static decimal? Dec(JsonElement? e, params string[] path) =>
        decimal.TryParse(Str(e, path), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>The shared secret the shop put in the callback address, compared in constant time.</summary>
    protected static bool SecretMatches(string? expected, string? presented)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(presented)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    protected static string Query(CarrierWebhookRequest request, string name) =>
        request.Query.TryGetValue(name, out var v) ? v : request.Query.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

    /// <summary>Whole VND, as every carrier here wants it.</summary>
    protected static long Vnd(decimal amount) => (long)Math.Round(amount, MidpointRounding.AwayFromZero);

    protected static string Truncate(string? text, int max) => string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max];
}
