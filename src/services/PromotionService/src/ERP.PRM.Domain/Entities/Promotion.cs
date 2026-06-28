using ERP.Shared.Core.Domain;
using ERP.PRM.Domain.Enums;
using ERP.PRM.Domain.Events;

namespace ERP.PRM.Domain.Entities;

public class Promotion : AggregateRoot
{
    private readonly List<Coupon> _coupons = new();

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public decimal MinOrderAmount { get; private set; }
    public int? MaxUsageTotal { get; private set; }
    public int? MaxUsagePerUser { get; private set; }
    public int CurrentUsage { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public PromotionStatus Status { get; private set; }
    public IReadOnlyCollection<Coupon> Coupons => _coupons.AsReadOnly();

    private Promotion() { }

    public static Promotion Create(string name, DiscountType discountType, decimal discountValue,
        DateTime startDate, DateTime endDate, decimal minOrderAmount = 0,
        int? maxUsage = null, string? description = null)
    {
        if (endDate <= startDate)
            throw new InvalidOperationException("End date must be after start date.");

        var promo = new Promotion
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DiscountType = discountType,
            DiscountValue = discountValue,
            MinOrderAmount = minOrderAmount,
            MaxUsageTotal = maxUsage,
            StartDate = startDate,
            EndDate = endDate,
            Status = PromotionStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        promo.AddDomainEvent(new PromotionCreatedEvent(promo.Id, name, discountType, discountValue, startDate, endDate));
        return promo;
    }

    public void Activate()
    {
        Status = PromotionStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Pause()
    {
        Status = PromotionStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    public Coupon GenerateCoupon(string code)
    {
        if (_coupons.Any(c => c.Code == code))
            throw new InvalidOperationException($"Coupon code '{code}' already exists.");

        var coupon = Coupon.Create(Id, code);
        _coupons.Add(coupon);
        return coupon;
    }

    public decimal CalculateDiscount(decimal orderAmount)
    {
        if (orderAmount < MinOrderAmount) return 0;
        if (!IsActive()) return 0;

        return DiscountType switch
        {
            DiscountType.Percentage => orderAmount * DiscountValue / 100,
            DiscountType.FixedAmount => Math.Min(DiscountValue, orderAmount),
            _ => 0
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
