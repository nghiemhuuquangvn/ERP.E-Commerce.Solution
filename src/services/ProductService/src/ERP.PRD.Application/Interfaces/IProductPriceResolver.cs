using ERP.PRD.Domain.Entities;

namespace ERP.PRD.Application.Interfaces
{
    /// <summary>A product's retail "from" price and the variant that carries it.</summary>
    public readonly record struct ResolvedProductPrice(decimal Price, Guid VariantId);

    /// <summary>
    /// What a variant / product sells for at retail right now, read from the price lists with <see cref="RetailPriceRules"/>
    /// (DEFERRED R111). A product or variant with no price is simply missing from the result (clients show 0 / "no price").
    /// </summary>
    public interface IProductPriceResolver
    {
        Task<Dictionary<Guid, decimal>> GetVariantPricesAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default);

        /// <summary>One query for the whole set (a page of products), cheapest variant per product.</summary>
        Task<Dictionary<Guid, ResolvedProductPrice>> GetProductPricesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
    }

    public static class ProductPriceEvents
    {
        /// <summary>
        /// Raises ProductPriceChanged on the product when its retail price differs between the two snapshots (taken before and after a price
        /// list change). Returns true when an event was raised.
        /// </summary>
        public static bool RaiseIfChanged(
            Product product,
            IReadOnlyDictionary<Guid, ResolvedProductPrice> before,
            IReadOnlyDictionary<Guid, ResolvedProductPrice> after)
        {
            var hadBefore = before.TryGetValue(product.Id, out var oldPrice);
            var hasAfter = after.TryGetValue(product.Id, out var newPrice);
            var oldValue = hadBefore ? oldPrice.Price : 0m;
            var newValue = hasAfter ? newPrice.Price : 0m;
            if (oldValue == newValue)
                return false;

            var variantId = hasAfter ? newPrice.VariantId : oldPrice.VariantId;
            product.RecordPriceChange(variantId, oldValue, newValue);
            return true;
        }
    }
}
