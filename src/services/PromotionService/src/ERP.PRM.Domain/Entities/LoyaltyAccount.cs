using ERP.Shared.Core.Domain;
using ERP.PRM.Domain.Enums;
using ERP.PRM.Domain.Events;

namespace ERP.PRM.Domain.Entities;

public class LoyaltyAccount : AggregateRoot
{
    public Guid UserId { get; private set; }
    public int TotalPoints { get; private set; }
    public int AvailablePoints { get; private set; }
    public LoyaltyTier Tier { get; private set; }

    private LoyaltyAccount() { }

    public static LoyaltyAccount Create(Guid userId)
    {
        return new LoyaltyAccount
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TotalPoints = 0,
            AvailablePoints = 0,
            Tier = LoyaltyTier.Bronze,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void EarnPoints(int points, string reason)
    {
        TotalPoints += points;
        AvailablePoints += points;
        UpdateTier();
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new LoyaltyPointsEarnedEvent(Id, UserId, points, AvailablePoints, reason));
    }

    public void RedeemPoints(int points)
    {
        if (points > AvailablePoints)
            throw new InvalidOperationException($"Insufficient points. Available: {AvailablePoints}, Requested: {points}");

        AvailablePoints -= points;
        UpdatedAt = DateTime.UtcNow;
    }

    private void UpdateTier()
    {
        Tier = TotalPoints switch
        {
            >= 10000 => LoyaltyTier.Platinum,
            >= 5000 => LoyaltyTier.Gold,
            >= 1000 => LoyaltyTier.Silver,
            _ => LoyaltyTier.Bronze
        };
    }
}
