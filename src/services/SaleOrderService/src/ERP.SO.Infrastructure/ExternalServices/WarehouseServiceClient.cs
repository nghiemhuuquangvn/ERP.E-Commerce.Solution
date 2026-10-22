using System.Text.Json;
using ERP.SO.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Infrastructure.ExternalServices
{
    public class WarehouseServiceClient : IWarehouseServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WarehouseServiceClient> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public WarehouseServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<WarehouseServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(configuration["Services:WarehouseServiceBaseUrl"] ?? "http://localhost:5004");
        }

        public async Task<StockAvailabilityResult> CheckAvailabilityAsync(Guid productId, int requestedQuantity, CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"internal/v1/inventory/{productId}/availability", ct);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // No Inventory row for this product yet — fail closed rather than
                    // assuming unlimited stock for a product WareHouseService has never heard of.
                    return new StockAvailabilityResult
                    {
                        IsAvailable = false,
                        AvailableQuantity = 0,
                        ErrorMessage = $"No inventory record found for product {productId}"
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("WareHouseService availability check returned {StatusCode} for product {ProductId}", response.StatusCode, productId);
                    return new StockAvailabilityResult
                    {
                        IsAvailable = false,
                        AvailableQuantity = 0,
                        ErrorMessage = $"WareHouseService returned {(int)response.StatusCode}"
                    };
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                var payload = JsonSerializer.Deserialize<AvailabilityPayload>(body, JsonOptions);
                var available = payload?.AvailableQuantity ?? 0;

                return new StockAvailabilityResult
                {
                    IsAvailable = available >= requestedQuantity,
                    AvailableQuantity = available
                };
            }
            catch (Exception ex)
            {
                // Fail closed: unlike coupon validation, an unreachable WareHouseService
                // must not be treated as "assume in stock" — that would silently
                // reintroduce the charge-then-refund problem this check exists to close.
                _logger.LogWarning(ex, "Failed to reach WareHouseService to check availability for product {ProductId}", productId);
                return new StockAvailabilityResult
                {
                    IsAvailable = false,
                    AvailableQuantity = 0,
                    ErrorMessage = "WareHouseService unavailable"
                };
            }
        }

        private class AvailabilityPayload
        {
            public Guid ProductId { get; set; }
            public int AvailableQuantity { get; set; }
        }
    }
}
