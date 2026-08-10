using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ERP.CHB.Application.Interfaces;

namespace ERP.CHB.Infrastructure.Gateway;

/// <summary>
/// HttpClient's BaseAddress is set to Gateway:BaseUrl (e.g. http://gateway:5000) by the
/// named-client registration in Program.cs, so every call here is a same-shape request
/// through Ocelot exactly like a browser would make — no direct service-to-service
/// shortcut, consistent with the "services must not assume the Gateway is the only path
/// in" note in docs/business/Identity/License.md but matching how tool calls are meant to
/// be authorized (via the forwarded bearer token, which Ocelot/each service still validates).
/// </summary>
public class ErpGatewayClient : IErpGatewayClient
{
    private readonly HttpClient _httpClient;

    public ErpGatewayClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<string> GetAsync(string relativePath, string? bearerToken, CancellationToken ct) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, relativePath), bearerToken, ct);

    public Task<string> PostAsync(string relativePath, object body, string? bearerToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, relativePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        return SendAsync(request, bearerToken, ct);
    }

    private async Task<string> SendAsync(HttpRequestMessage request, string? bearerToken, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        // Returned as tool-result content, not thrown — a 403/404 from the downstream
        // service is a legitimate answer the model should reason about (e.g. "the caller
        // doesn't have access" or "no such order"), not an exception that aborts the turn.
        if (!response.IsSuccessStatusCode)
            return JsonSerializer.Serialize(new { error = $"Upstream returned {(int)response.StatusCode}", detail = Truncate(content) });

        return Truncate(content);
    }

    private static string Truncate(string content) =>
        content.Length > 8000 ? content[..8000] : content;
}
