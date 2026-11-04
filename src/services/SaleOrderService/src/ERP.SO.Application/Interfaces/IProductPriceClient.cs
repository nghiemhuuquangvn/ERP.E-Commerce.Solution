namespace ERP.SO.Application.Interfaces
{
    /// <summary>What products sell for at retail, asked of ProductService (service token, the order's tenant). Fails closed: null means "ProductService could not answer".</summary>
    public interface IProductPriceClient
    {
        /// <summary>Retail price per product; a product without a price is missing from the result.</summary>
        Task<IReadOnlyDictionary<Guid, decimal>?> GetPricesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
    }
}
