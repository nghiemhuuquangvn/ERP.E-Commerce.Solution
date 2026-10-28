namespace ERP.SO.Application.Interfaces
{
    /// <summary>
    /// Synchronous read of what the shop charges for delivery (ShippingService owns the fee and its free-shipping threshold, ROADMAP P1-11b).
    /// Fails closed like the stock check: null means "could not be worked out" and the order is refused rather than saved with a guessed fee.
    /// </summary>
    public interface IShippingServiceClient
    {
        /// <param name="merchandiseValue">The goods after any coupon, without delivery.</param>
        /// <param name="province">The delivery province (the order's State): the shop's price list is by province.</param>
        /// <param name="items">Product and quantity of each line: the price list is by parcel weight.</param>
        Task<decimal?> GetFeeAsync(decimal merchandiseValue, string? province, IReadOnlyCollection<(Guid ProductId, int Quantity)> items, CancellationToken ct);
    }
}
