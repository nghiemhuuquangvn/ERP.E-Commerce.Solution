using ERP.Shared.Core.Domain;
using ERP.PRM.Domain.Enums;
using ERP.PRM.Domain.Events;

namespace ERP.PRM.Domain.Entities;

public class LoyaltyAccount : AggregateRoot
{
    private readonly List<LoyaltyTransaction> _transactions = new();

    public Guid UserId { get; private set; }
    public int TotalPoints { get; private set; }
    public int AvailablePoints { get; private set; }
    public LoyaltyTier Tier { get; private set; }
    public IReadOnlyCollection<LoyaltyTransaction> Transactions => _transactions.AsReadOnly();

    // Tier thresholds (TotalPoints earned lifetime)
    public static readonly int SilverThreshold = 1000;
    public static readonly int GoldThreshold = 5000;
    public static readonly int PlatinumThreshold = 10000;

    // Points earned per currency unit by tier
    public static int PointsPerUnit(LoyaltyTier tier) => tier switch
    {
        LoyaltyTier.Bronze => 1,
        LoyaltyTier.Silver => 2,
        LoyaltyTier.Gold => 3,
        LoyaltyTier.Platinum => 5,
        _ => 1
    };

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
        if (points <= 0) throw new InvalidOperationException("Points must be positive.");

        TotalPoints += points;
        AvailablePoints += points;
        UpdateTier();
        UpdatedAt = DateTime.UtcNow;

        _transactions.Add(LoyaltyTransaction.CreateEarn(Id, UserId, points, reason));
        AddDomainEvent(new LoyaltyPointsEarnedEvent(Id, UserId, points, AvailablePoints, reason));
    }

    public void RedeemPoints(int points)
    {
        if (points <= 0) throw new InvalidOperationException("Points must be positive.");
        if (points > AvailablePoints)
            throw new InvalidOperationException($"Insufficient points. Available: {AvailablePoints}, Requested: {points}");

        AvailablePoints -= points;
        UpdatedAt = DateTime.UtcNow;

        _transactions.Add(LoyaltyTransaction.CreateRedeem(Id, UserId, points));
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
