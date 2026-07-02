using ERP.Shared.Core.Domain;
using ERP.PRM.Domain.Events;

namespace ERP.PRM.Domain.Entities;

public class Coupon : BaseEntity
{
    public Guid PromotionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public string? UsedByUserId { get; private set; }

    private Coupon() { }

    public static Coupon Create(Guid promotionId, string code)
    {
        return new Coupon
        {
            Id = Guid.NewGuid(),
            PromotionId = promotionId,
            Code = code.ToUpperInvariant(),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Redeem(string userId, decimal discountAmount)
    {
        if (IsUsed)
            throw new InvalidOperationException("Coupon has already been used.");

        IsUsed = true;
        UsedAt = DateTime.UtcNow;
        UsedByUserId = userId;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new CouponRedeemedEvent(Id, PromotionId, Code, userId, discountAmount));
    }
}
