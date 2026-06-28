using ERP.Shared.Core.Domain;
using ERP.PRM.Domain.Enums;

namespace ERP.PRM.Domain.Events;

public record PromotionCreatedEvent(
    Guid PromotionId, string Name, DiscountType DiscountType,
    decimal DiscountValue, DateTime StartDate, DateTime EndDate) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CouponRedeemedEvent(
    Guid CouponId, Guid PromotionId, string Code, string UserId, decimal DiscountAmount) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record LoyaltyPointsEarnedEvent(
    Guid AccountId, Guid UserId, int PointsEarned, int TotalAvailable, string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
