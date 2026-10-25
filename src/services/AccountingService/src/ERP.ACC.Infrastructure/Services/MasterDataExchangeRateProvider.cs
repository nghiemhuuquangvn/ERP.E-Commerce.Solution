using System.Globalization;
using System.Net;
using System.Text.Json;
using ERP.ACC.Application.Fx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Infrastructure.Services;

/// <summary>
/// Reads the exchange rate the tenant entered in SharedMasterDataService, through its /internal route with this service's own token (ROADMAP P1-4).
/// A rate is only a suggestion for a foreign document: any failure answers "no rate known" and the user types it.
/// </summary>
public class MasterDataExchangeRateProvider : IExchangeRateProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly ILogger<MasterDataExchangeRateProvider> _logger;

    public MasterDataExchangeRateProvider(HttpClient http, IConfiguration configuration, ILogger<MasterDataExchangeRateProvider> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri(configuration["Services:MasterDataServiceBaseUrl"] ?? "http://localhost:5015");
    }

    public async Task<ExchangeRateQuote?> GetRateAsync(string from, string to, DateTime asOf, CancellationToken ct = default)
    {
        try
        {
            var url = $"internal/v1/exchange-rates/effective?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}&asOf={asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            using var response = await _http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("The exchange rate {From}/{To} could not be read: MasterData answered {Status}", from, to, (int)response.StatusCode);
                return null;
            }
            var body = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<RateDto>(body, JsonOptions);
            return dto is { Rate: > 0m } ? new ExchangeRateQuote(dto.From ?? from, dto.To ?? to, dto.Rate, dto.EffectiveDate, dto.Inverted) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "The exchange rate {From}/{To} could not be read from MasterData", from, to);
            return null;
        }
    }

    private sealed record RateDto(string? From, string? To, decimal Rate, DateTime EffectiveDate, bool Inverted);
}
