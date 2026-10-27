namespace ERP.SO.Application.Interfaces
{
    /// <summary>
    /// Synchronous read of what the shop charges for delivery (ShippingService owns the fee and its free-shipping threshold, ROADMAP P1-11b).
    /// Fails closed like the stock check: null means "could not be worked out" and the order is refused rather than saved with a guessed fee.
    /// </summary>
    public interface IShippingServiceClient
    {
        /// <param name="merchandiseValue">The goods after any coupon, without delivery.</param>
        Task<decimal?> GetFeeAsync(decimal merchandiseValue, CancellationToken ct);
    }
}
