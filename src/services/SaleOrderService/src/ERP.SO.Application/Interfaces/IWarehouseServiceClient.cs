namespace ERP.SO.Application.Interfaces
{
    /// <summary>
    /// Synchronous read-only call to WareHouseService to check stock availability
    /// before an order is persisted and payment is initiated. Unlike
    /// IPromotionServiceClient (where an outage just means "no discount applied"),
    /// this MUST fail closed: if availability can't be confirmed, order creation is
    /// rejected rather than silently assumed to be in stock — the whole point of this
    /// client is to stop charging customers for stock we can't verify exists.
    /// </summary>
    public interface IWarehouseServiceClient
    {
        Task<StockAvailabilityResult> CheckAvailabilityAsync(Guid productId, int requestedQuantity, CancellationToken ct);
    }

    public class StockAvailabilityResult
    {
        public bool IsAvailable { get; set; }
        public int AvailableQuantity { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
