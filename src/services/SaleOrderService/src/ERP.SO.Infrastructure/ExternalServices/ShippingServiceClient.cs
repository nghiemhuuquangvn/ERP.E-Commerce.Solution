using System.Globalization;
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

        public async Task<decimal?> GetFeeAsync(decimal merchandiseValue, CancellationToken ct)
        {
            try
            {
                var query = merchandiseValue.ToString(CultureInfo.InvariantCulture);
                using var response = await _httpClient.GetAsync($"internal/v1/shipping-fee?merchandiseValue={query}", ct);
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
