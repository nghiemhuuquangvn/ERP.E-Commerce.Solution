using System.Net.Http.Json;
using System.Text.Json;
using ERP.SO.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Infrastructure.ExternalServices
{
    public class ShippingServiceClient : IShippingServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ShippingServiceClient> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public ShippingServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<ShippingServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(configuration["Services:ShippingServiceBaseUrl"] ?? "http://localhost:5010");
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        public async Task<decimal?> GetFeeAsync(decimal merchandiseValue, string? province, IReadOnlyCollection<(Guid ProductId, int Quantity)> items, CancellationToken ct)
        {
            try
            {
                var body = new { merchandiseValue, province, items = items.Select(i => new { productId = i.ProductId, quantity = i.Quantity }).ToList() };
                using var response = await _httpClient.PostAsJsonAsync("internal/v1/shipping-fee", body, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ShippingService returned {StatusCode} for the delivery fee", (int)response.StatusCode);
                    return null;
                }
                var payload = JsonSerializer.Deserialize<FeePayload>(await response.Content.ReadAsStringAsync(ct), JsonOptions);
                return payload is { Fee: >= 0 } ? payload.Fee : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Failed to reach ShippingService for the delivery fee");
                return null;
            }
        }

        private class FeePayload
        {
            public decimal Fee { get; set; }
        }
    }
}
