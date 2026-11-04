using ERP.PRD.Domain.Enums;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>One price-list row of one variant, with what the rule needs to know about its list.</summary>
    public readonly record struct PriceCandidate(
        Guid VariantId,
        PriceListChannel Channel,
        bool ListIsActive,
        DateTime EffectiveDate,
        DateTime? ExpiryDate,
        int MinQuantity,
        decimal BasePrice,
        decimal? DiscountedPrice);

    /// <summary>
    /// The one rule that says what a variant (and so a product) sells for at retail. Elasticsearch, the product
    /// DTOs and the price-changed event all use it, so every client sees the same figure (DEFERRED R111).
    /// </summary>
    public static class RetailPriceRules
    {
        /// <summary>Retail list first, then Ecommerce; a Wholesale list is never a retail price.</summary>
        public static int? ChannelRank(PriceListChannel channel) => channel switch
        {
            PriceListChannel.Retail => 0,
            PriceListChannel.Ecommerce => 1,
            _ => null
        };

        public static bool IsCurrent(PriceCandidate c, DateTime now)
            => c.ListIsActive && c.EffectiveDate <= now && (c.ExpiryDate == null || c.ExpiryDate > now);

        /// <summary>
        /// The retail price of ONE variant: among the rows of active, currently effective lists, take the best channel, then the newest list
        /// (EffectiveDate), then the lowest quantity tier; the price is the discounted price when there is one. Null = no retail price.
        /// </summary>
        public static decimal? PickVariantPrice(IEnumerable<PriceCandidate> candidates, DateTime now)
        {
            var best = candidates
                .Where(c => IsCurrent(c, now) && ChannelRank(c.Channel) != null)
                .OrderBy(c => ChannelRank(c.Channel))
                .ThenByDescending(c => c.EffectiveDate)
                .ThenBy(c => c.MinQuantity)
                .Cast<PriceCandidate?>()
                .FirstOrDefault();

            return best == null ? null : best.Value.DiscountedPrice ?? best.Value.BasePrice;
        }

        /// <summary>A product's "from" price: its cheapest priced variant. Null = no variant has a price.</summary>
        public static decimal? ProductPrice(IEnumerable<decimal?> variantPrices)
        {
            var priced = variantPrices.Where(p => p.HasValue).Select(p => p!.Value).ToList();
            return priced.Count == 0 ? null : priced.Min();
        }
    }
}
