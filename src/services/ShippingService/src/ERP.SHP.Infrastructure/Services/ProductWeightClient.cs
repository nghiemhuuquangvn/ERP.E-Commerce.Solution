using System.Net.Http.Json;
using ERP.SHP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Infrastructure.Services;

/// <summary>Calls ProductService's internal weights endpoint with this service's own token (the order's tenant travels in it).</summary>
public class ProductWeightClient : IProductWeightClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ProductWeightClient> _logger;

    public ProductWeightClient(HttpClient http, IConfiguration configuration, ILogger<ProductWeightClient> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri(configuration["Services:ProductServiceBaseUrl"] ?? "http://localhost:5008");
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetWeightsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var empty = new Dictionary<Guid, int>();
        if (productIds.Count == 0) return empty;
        try
        {
            using var response = await _http.PostAsJsonAsync("internal/v1/product-weights", new { productIds = productIds.Distinct().ToList() }, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ProductService returned {Status} for the product weights; the default item weight is used", (int)response.StatusCode);
                return empty;
            }
            var body = await response.Content.ReadFromJsonAsync<WeightsResponse>(cancellationToken: ct);
            return (body?.Weights ?? new()).Where(w => w.Grams > 0).GroupBy(w => w.ProductId).ToDictionary(g => g.Key, g => g.First().Grams);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "ProductService could not be reached for the product weights; the default item weight is used");
            return empty;
        }
    }

    private class WeightsResponse
    {
        public List<WeightRow> Weights { get; set; } = new();
    }

    private class WeightRow
    {
        public Guid ProductId { get; set; }
        public int Grams { get; set; }
    }
}
