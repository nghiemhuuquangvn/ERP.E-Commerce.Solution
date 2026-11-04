using System.Net.Http.Json;
using System.Text.Json;
using ERP.SO.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Infrastructure.ExternalServices
{
    /// <summary>Asks ProductService (<c>internal/v1/product-prices</c>) what products sell for. Fails closed: any problem returns null so the order is refused instead of priced by guess (R113).</summary>
    public class ProductPriceClient : IProductPriceClient
    {
        private const int MaxPerCall = 200;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductPriceClient> _logger;

        public ProductPriceClient(HttpClient httpClient, IConfiguration configuration, ILogger<ProductPriceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(configuration["Services:ProductServiceBaseUrl"] ?? "http://localhost:5008");
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        public async Task<IReadOnlyDictionary<Guid, decimal>?> GetPricesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
        {
            var result = new Dictionary<Guid, decimal>();
            try
            {
                foreach (var chunk in productIds.Distinct().Chunk(MaxPerCall))
                {
                    using var response = await _httpClient.PostAsJsonAsync("internal/v1/product-prices", new { productIds = chunk }, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("ProductService returned {StatusCode} for the product prices", (int)response.StatusCode);
                        return null;
                    }

                    var payload = JsonSerializer.Deserialize<PricesPayload>(await response.Content.ReadAsStringAsync(ct), JsonOptions);
                    if (payload?.Prices == null)
                        return null;
                    foreach (var p in payload.Prices)
                        result[p.ProductId] = p.Price;
                }

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Failed to reach ProductService for the product prices");
                return null;
            }
        }

        private class PricesPayload
        {
            public List<PriceRow>? Prices { get; set; }
        }

        private class PriceRow
        {
            public Guid ProductId { get; set; }
            public decimal Price { get; set; }
        }
    }
}
