using ERP.Shared.Core.Domain;

namespace ERP.PRM.Domain.Entities;

public class LoyaltyTransaction : BaseEntity
{
    public Guid AccountId { get; private set; }
    public Guid UserId { get; private set; }
    public int Points { get; private set; }        // positive = earned, negative = redeemed
    public string Reason { get; private set; } = string.Empty;
    public string TransactionType { get; private set; } = string.Empty; // Earn | Redeem | Expire

    private LoyaltyTransaction() { }

    public static LoyaltyTransaction CreateEarn(Guid accountId, Guid userId, int points, string reason) =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            Points = points,
            Reason = reason,
            TransactionType = "Earn",
            CreatedAt = DateTime.UtcNow
        };

    public static LoyaltyTransaction CreateRedeem(Guid accountId, Guid userId, int points) =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            Points = -points,
            Reason = "Points redeemed",
            TransactionType = "Redeem",
            CreatedAt = DateTime.UtcNow
        };
}
