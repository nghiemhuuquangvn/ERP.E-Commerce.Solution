using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Pricing
{
    /// <summary>
    /// Reads the shop's price lists (tenant filter on the context) and applies <see cref="RetailPriceRules"/>; two queries per call however many
    /// products are asked about, so a product page does not cost one query per product.
    /// </summary>
    internal sealed class ProductPriceResolver : IProductPriceResolver
    {
        private readonly ProductDbContext _db;

        public ProductPriceResolver(ProductDbContext db) => _db = db;

        public async Task<Dictionary<Guid, decimal>> GetVariantPricesAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default)
        {
            var result = new Dictionary<Guid, decimal>();
            if (variantIds.Count == 0)
                return result;

            var now = DateTime.UtcNow;
            var rows = await _db.PriceLists
                .Where(pl => pl.IsActive && pl.EffectiveDate <= now && (pl.ExpiryDate == null || pl.ExpiryDate > now))
                .SelectMany(pl => pl.Items.Select(i => new
                {
                    i.VariantId, pl.Channel, pl.IsActive, pl.EffectiveDate, pl.ExpiryDate, i.MinQuantity, i.BasePrice, i.DiscountedPrice
                }))
                .Where(x => variantIds.Contains(x.VariantId))
                .ToListAsync(ct);

            foreach (var group in rows.GroupBy(r => r.VariantId))
            {
                var price = RetailPriceRules.PickVariantPrice(
                    group.Select(r => new PriceCandidate(r.VariantId, r.Channel, r.IsActive, r.EffectiveDate, r.ExpiryDate, r.MinQuantity, r.BasePrice, r.DiscountedPrice)),
                    now);
                if (price.HasValue)
                    result[group.Key] = price.Value;
            }

            return result;
        }

        public async Task<Dictionary<Guid, ResolvedProductPrice>> GetProductPricesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
        {
            var result = new Dictionary<Guid, ResolvedProductPrice>();
            if (productIds.Count == 0)
                return result;

            var variants = await _db.ProductVariants
                .Where(v => productIds.Contains(v.ProductId) && !v.IsDeleted)
                .Select(v => new { v.Id, v.ProductId })
                .ToListAsync(ct);

            var prices = await GetVariantPricesAsync(variants.Select(v => v.Id).ToList(), ct);

            foreach (var group in variants.GroupBy(v => v.ProductId))
            {
                Guid? bestVariant = null;
                var bestPrice = 0m;
                foreach (var v in group)
                {
                    if (prices.TryGetValue(v.Id, out var price) && (bestVariant == null || price < bestPrice))
                    {
                        bestVariant = v.Id;
                        bestPrice = price;
                    }
                }

                if (bestVariant != null)
                    result[group.Key] = new ResolvedProductPrice(bestPrice, bestVariant.Value);
            }

            return result;
        }
    }
}
