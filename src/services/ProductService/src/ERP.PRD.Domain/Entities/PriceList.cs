using ERP.PRD.Domain.Enums;
using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Price list containing pricing rules for product variants.
    /// Supports multiple price lists per product with activation dates.
    /// </summary>
    public class PriceList : AggregateRoot
    {
        public string Name { get; set; } = string.Empty;
        public new string? Description { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public PriceListChannel Channel { get; set; } = PriceListChannel.Retail;

        /// <summary>
        /// References SharedMasterDataService's Currency by Id only (cross-service,
        /// no database FK — see CLAUDE.md database ownership rules). Null means the
        /// tenant's default currency applies.
        /// </summary>
        public Guid? CurrencyId { get; set; }

        private readonly List<PriceListItem> _items = new();
        public IReadOnlyList<PriceListItem> Items => _items.AsReadOnly();

        public static PriceList Create(
            string name,
            DateTime effectiveDate,
            bool isActive = true,
            PriceListChannel channel = PriceListChannel.Retail,
            Guid? currencyId = null)
        {
            var priceList = new PriceList
            {
                Id = Guid.NewGuid(),
                Name = name,
                EffectiveDate = effectiveDate,
                IsActive = isActive,
                Channel = channel,
                CurrencyId = currencyId,
                CreatedAt = DateTime.UtcNow
            };

            return priceList;
        }

        public void SetChannelAndCurrency(PriceListChannel channel, Guid? currencyId)
        {
            Channel = channel;
            CurrencyId = currencyId;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Quantity-break pricing: a variant may have multiple rows in the same price
        /// list, each starting at a different MinQuantity tier (e.g. 1 unit vs. 10+
        /// units). Uniqueness is therefore on (VariantId, MinQuantity), not VariantId
        /// alone.
        /// </summary>
        public void AddItem(Guid variantId, decimal basePrice, decimal? discountedPrice = null, int minQuantity = 1)
        {
            if (_items.Any(i => i.VariantId == variantId && i.MinQuantity == minQuantity))
                throw new InvalidOperationException(
                    $"Price already defined for variant {variantId} at quantity {minQuantity}");

            if (basePrice < 0)
                throw new ArgumentException("Base price cannot be negative");

            if (discountedPrice.HasValue && discountedPrice < 0)
                throw new ArgumentException("Discounted price cannot be negative");

            if (minQuantity < 1)
                throw new ArgumentException("MinQuantity must be at least 1");

            _items.Add(new PriceListItem
            {
                Id = Guid.NewGuid(),
                VariantId = variantId,
                BasePrice = basePrice,
                DiscountedPrice = discountedPrice,
                MinQuantity = minQuantity,
                CreatedAt = DateTime.UtcNow
            });
        }

        public void UpdatePrice(Guid variantId, decimal basePrice, decimal? discountedPrice = null, int minQuantity = 1)
        {
            var item = _items.FirstOrDefault(i => i.VariantId == variantId && i.MinQuantity == minQuantity);
            if (item == null)
                throw new InvalidOperationException($"Price not found for variant {variantId} at quantity {minQuantity}");

            item.BasePrice = basePrice;
            item.DiscountedPrice = discountedPrice;
            item.UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetExpiry(DateTime expiryDate)
        {
            ExpiryDate = expiryDate;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Individual price entry in a price list for a specific variant, at a given
    /// quantity-break tier (MinQuantity).
    /// </summary>
    public class PriceListItem
    {
        public Guid Id { get; set; }
        public Guid VariantId { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public int MinQuantity { get; set; } = 1;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public decimal EffectivePrice => DiscountedPrice ?? BasePrice;
    }
}
