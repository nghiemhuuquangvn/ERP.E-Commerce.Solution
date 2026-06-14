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
        public string? Description { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }

        private readonly List<PriceListItem> _items = new();
        public IReadOnlyList<PriceListItem> Items => _items.AsReadOnly();

        public static PriceList Create(string name, DateTime effectiveDate, bool isActive = true)
        {
            var priceList = new PriceList
            {
                Id = Guid.NewGuid(),
                Name = name,
                EffectiveDate = effectiveDate,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            };

            return priceList;
        }

        public void AddItem(Guid variantId, decimal basePrice, decimal? discountedPrice = null)
        {
            if (_items.Any(i => i.VariantId == variantId))
                throw new InvalidOperationException($"Price already defined for variant {variantId}");

            if (basePrice < 0)
                throw new ArgumentException("Base price cannot be negative");

            if (discountedPrice.HasValue && discountedPrice < 0)
                throw new ArgumentException("Discounted price cannot be negative");

            _items.Add(new PriceListItem
            {
                Id = Guid.NewGuid(),
                VariantId = variantId,
                BasePrice = basePrice,
                DiscountedPrice = discountedPrice,
                CreatedAt = DateTime.UtcNow
            });
        }

        public void UpdatePrice(Guid variantId, decimal basePrice, decimal? discountedPrice = null)
        {
            var item = _items.FirstOrDefault(i => i.VariantId == variantId);
            if (item == null)
                throw new InvalidOperationException($"Price not found for variant {variantId}");

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
    /// Individual price entry in a price list for a specific variant.
    /// </summary>
    public class PriceListItem
    {
        public Guid Id { get; set; }
        public Guid VariantId { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public decimal EffectivePrice => DiscountedPrice ?? BasePrice;
    }
}
