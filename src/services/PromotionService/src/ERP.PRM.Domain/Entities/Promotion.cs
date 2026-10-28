using ERP.Shared.Core.Domain;
using ERP.PRM.Domain.Enums;
using ERP.PRM.Domain.Events;

namespace ERP.PRM.Domain.Entities;

public class Promotion : AggregateRoot
{
    private readonly List<Coupon> _coupons = new();

    public string Name { get; private set; } = string.Empty;
    public new string? Description { get; private set; }
    public DiscountType DiscountType { get; private set; }
    public PromotionScope Scope { get; private set; }
    public decimal DiscountValue { get; private set; }
    public decimal? MaxDiscountAmount { get; private set; }
    public decimal MinOrderAmount { get; private set; }
    public int? MaxUsageTotal { get; private set; }
    public int? MaxUsagePerUser { get; private set; }
    public int CurrentUsage { get; private set; }
    public int? BuyQuantity { get; private set; }
    public int? GetQuantity { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public PromotionStatus Status { get; private set; }

    // EF Core maps these as JSON columns; private set prevents external replacement
    public List<Guid> ApplicableProductIds { get; private set; } = new();
    public List<string> ApplicableCategoryIds { get; private set; } = new();

    public IReadOnlyCollection<Coupon> Coupons => _coupons.AsReadOnly();

    private Promotion() { }

    public static Promotion Create(string name, DiscountType discountType, decimal discountValue,
        DateTime startDate, DateTime endDate, decimal minOrderAmount = 0,
        int? maxUsage = null, int? maxUsagePerUser = null, string? description = null,
        PromotionScope scope = PromotionScope.Order, decimal? maxDiscountAmount = null,
        int? buyQuantity = null, int? getQuantity = null)
    {
        if (endDate <= startDate)
            throw new InvalidOperationException("End date must be after start date.");

        if (discountType == DiscountType.BuyXGetY && (buyQuantity == null || getQuantity == null))
            throw new InvalidOperationException("BuyQuantity and GetQuantity are required for BuyXGetY promotions.");

        var promo = new Promotion
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DiscountType = discountType,
            DiscountValue = discountValue,
            Scope = scope,
            MinOrderAmount = minOrderAmount,
            MaxUsageTotal = maxUsage,
            MaxUsagePerUser = maxUsagePerUser,
            MaxDiscountAmount = maxDiscountAmount,
            BuyQuantity = buyQuantity,
            GetQuantity = getQuantity,
            StartDate = startDate,
            EndDate = endDate,
            Status = PromotionStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        promo.AddDomainEvent(new PromotionCreatedEvent(promo.Id, name, discountType, discountValue, startDate, endDate));
        return promo;
    }

    /// <summary>The back office may change a promotion only while it is a draft: once it is live, customers and orders already rely on its terms.</summary>
    public void Update(string name, string? description, DiscountType discountType, PromotionScope scope, decimal discountValue,
        decimal? maxDiscountAmount, decimal minOrderAmount, int? maxUsageTotal, int? maxUsagePerUser,
        int? buyQuantity, int? getQuantity, DateTime startDate, DateTime endDate,
        IEnumerable<Guid> productIds, IEnumerable<string> categoryIds)
    {
        if (Status != PromotionStatus.Draft)
            throw new InvalidOperationException("Only a draft promotion can be edited.");
        if (endDate <= startDate)
            throw new InvalidOperationException("End date must be after start date.");
        if (discountType == DiscountType.BuyXGetY && (buyQuantity == null || getQuantity == null))
            throw new InvalidOperationException("BuyQuantity and GetQuantity are required for BuyXGetY promotions.");

        Name = name;
        Description = description;
        DiscountType = discountType;
        Scope = scope;
        DiscountValue = discountValue;
        MaxDiscountAmount = maxDiscountAmount;
        MinOrderAmount = minOrderAmount;
        MaxUsageTotal = maxUsageTotal;
        MaxUsagePerUser = maxUsagePerUser;
        BuyQuantity = buyQuantity;
        GetQuantity = getQuantity;
        StartDate = startDate;
        EndDate = endDate;
        ApplicableProductIds = productIds.Distinct().ToList();
        ApplicableCategoryIds = categoryIds.Distinct().ToList();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        if (Status == PromotionStatus.Cancelled)
            throw new InvalidOperationException("Cannot activate a cancelled promotion.");
        Status = PromotionStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Pause()
    {
        Status = PromotionStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        Status = PromotionStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = PromotionStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddApplicableProduct(Guid productId)
    {
        if (!ApplicableProductIds.Contains(productId))
            ApplicableProductIds.Add(productId);
    }

    public void AddApplicableCategory(string category)
    {
        if (!ApplicableCategoryIds.Contains(category))
            ApplicableCategoryIds.Add(category);
    }

    public Coupon GenerateCoupon(string code)
    {
        if (_coupons.Any(c => c.Code == code))
            throw new InvalidOperationException($"Coupon code '{code}' already exists.");

        var coupon = Coupon.Create(Id, code);
        _coupons.Add(coupon);
        return coupon;
    }

    public List<Coupon> GenerateBulkCoupons(int count, string prefix = "")
    {
        var generated = new List<Coupon>();
        int attempts = 0;
        while (generated.Count < count && attempts < count * 3)
        {
            attempts++;
            var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var code = string.IsNullOrEmpty(prefix) ? suffix : $"{prefix.ToUpperInvariant()}-{suffix}";
            if (_coupons.Any(c => c.Code == code)) continue;
            var coupon = Coupon.Create(Id, code);
            _coupons.Add(coupon);
            generated.Add(coupon);
        }
        return generated;
    }

    public decimal CalculateDiscount(decimal orderAmount, List<(Guid ProductId, string Category, int Quantity, decimal UnitPrice)>? items = null)
    {
        if (orderAmount < MinOrderAmount) return 0;
        if (!IsActive()) return 0;

        decimal discount = DiscountType switch
        {
            DiscountType.Percentage => CalculatePercentageDiscount(orderAmount, items),
            DiscountType.FixedAmount => Math.Min(DiscountValue, orderAmount),
            DiscountType.BuyXGetY => CalculateBuyXGetYDiscount(items),
            DiscountType.FreeShipping => 0,
            _ => 0
        };

        if (MaxDiscountAmount.HasValue)
            discount = Math.Min(discount, MaxDiscountAmount.Value);

        return discount;
    }

    private decimal CalculatePercentageDiscount(decimal orderAmount, List<(Guid ProductId, string Category, int Quantity, decimal UnitPrice)>? items)
    {
        if (Scope == PromotionScope.Order || items == null)
            return orderAmount * DiscountValue / 100;

        return GetApplicableAmount(items) * DiscountValue / 100;
    }

    private decimal CalculateBuyXGetYDiscount(List<(Guid ProductId, string Category, int Quantity, decimal UnitPrice)>? items)
    {
        if (items == null || BuyQuantity == null || GetQuantity == null) return 0;

        var applicableItems = GetApplicableItems(items)
            .OrderBy(i => i.UnitPrice)
            .ToList();

        int totalQty = applicableItems.Sum(i => i.Quantity);
        if (totalQty < BuyQuantity) return 0;

        int sets = totalQty / (BuyQuantity.Value + GetQuantity.Value);
        int freeQty = sets * GetQuantity.Value;

        decimal discount = 0;
        foreach (var item in applicableItems)
        {
            if (freeQty <= 0) break;
            int free = Math.Min(freeQty, item.Quantity);
            discount += free * item.UnitPrice;
            freeQty -= free;
        }
        return discount;
    }

    private decimal GetApplicableAmount(List<(Guid ProductId, string Category, int Quantity, decimal UnitPrice)> items)
        => GetApplicableItems(items).Sum(i => i.Quantity * i.UnitPrice);

    private IEnumerable<(Guid ProductId, string Category, int Quantity, decimal UnitPrice)> GetApplicableItems(
        List<(Guid ProductId, string Category, int Quantity, decimal UnitPrice)> items)
    {
        return Scope switch
        {
            PromotionScope.Product when ApplicableProductIds.Count > 0
                => items.Where(i => ApplicableProductIds.Contains(i.ProductId)),
            PromotionScope.Category when ApplicableCategoryIds.Count > 0
                => items.Where(i => ApplicableCategoryIds.Contains(i.Category)),
            _ => items
        };
    }

    public bool IsApplicableToItems(List<(Guid ProductId, string Category, int Quantity, decimal UnitPrice)> items)
    {
        if (!IsActive()) return false;

        return Scope switch
        {
            PromotionScope.Product when ApplicableProductIds.Count > 0
                => items.Any(i => ApplicableProductIds.Contains(i.ProductId)),
            PromotionScope.Category when ApplicableCategoryIds.Count > 0
                => items.Any(i => ApplicableCategoryIds.Contains(i.Category)),
            _ => true
        };
    }

    public bool IsActive() =>
        Status == PromotionStatus.Active &&
        DateTime.UtcNow >= StartDate &&
        DateTime.UtcNow <= EndDate &&
        (MaxUsageTotal == null || CurrentUsage < MaxUsageTotal);

    public void RecordUsage()
    {
        if (!IsActive())
            throw new InvalidOperationException("Promotion is not active.");

        CurrentUsage++;
        UpdatedAt = DateTime.UtcNow;
    }
}
