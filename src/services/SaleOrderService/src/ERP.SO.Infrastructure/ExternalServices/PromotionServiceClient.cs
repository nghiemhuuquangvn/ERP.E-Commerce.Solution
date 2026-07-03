using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ERP.SO.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Infrastructure.ExternalServices
{
    public class PromotionServiceClient : IPromotionServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PromotionServiceClient> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public PromotionServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<PromotionServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(configuration["Services:PromotionServiceBaseUrl"] ?? "http://localhost:5012");
        }

        public async Task<CouponValidationResult> ValidateCouponAsync(string code, decimal orderAmount, CancellationToken ct)
        {
            try
            {
                var payload = JsonSerializer.Serialize(new { Code = code, OrderAmount = orderAmount });
                using var content = new StringContent(payload, Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var response = await _httpClient.PostAsync("api/v1/promotions/validate-coupon", content, ct);
                if (!response.IsSuccessStatusCode)
                    return new CouponValidationResult { IsValid = false, ErrorMessage = $"PromotionService returned {(int)response.StatusCode}" };

                var body = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<CouponValidationResult>(body, JsonOptions);
                return result ?? new CouponValidationResult { IsValid = false, ErrorMessage = "Empty response from PromotionService" };
            }
            catch (Exception ex)
            {
                // A coupon-validation outage should not block order creation entirely —
                // treat it as "no discount applied" rather than failing the whole order.
                _logger.LogWarning(ex, "Failed to validate coupon '{Code}' with PromotionService", code);
                return new CouponValidationResult { IsValid = false, ErrorMessage = "PromotionService unavailable" };
            }
        }
    }
}
