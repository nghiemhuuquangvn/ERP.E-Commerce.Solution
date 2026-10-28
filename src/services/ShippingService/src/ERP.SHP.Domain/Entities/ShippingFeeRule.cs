using ERP.Shared.Core.Domain;
using ERP.SHP.Domain.Text;

namespace ERP.SHP.Domain.Entities;

/// <summary>
/// One row of the shop's delivery price list (ROADMAP P1-11c): "parcels to these provinces in this weight band cost this much".
/// Active rows are tried in ascending <see cref="Priority"/>; the first whose conditions hold sets the fee. Every condition is optional (empty = any).
/// A parcel no row matches pays the shop's flat fee. The fee is the shop's price, not the carrier's cost.
/// </summary>
public class ShippingFeeRule : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>Comma separated province names; compared without case, diacritics or "Tỉnh / Thành phố".</summary>
    public string? MatchProvinces { get; private set; }
    /// <summary>Inclusive lower bound of the parcel weight in grams.</summary>
    public int? MinWeightGrams { get; private set; }
    /// <summary>Inclusive upper bound of the parcel weight in grams.</summary>
    public int? MaxWeightGrams { get; private set; }
    public decimal Fee { get; private set; }

    protected ShippingFeeRule() { }

    public static ShippingFeeRule Create(Guid tenantId, string name, int priority, decimal fee)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required", nameof(tenantId));
        var rule = new ShippingFeeRule { Id = Guid.NewGuid(), TenantId = tenantId, CreatedBy = "System", CreatedAt = DateTime.UtcNow };
        rule.Update(name, priority, true, null, null, null, fee);
        return rule;
    }

    public void Update(string name, int priority, bool isActive, string? matchProvinces, int? minWeightGrams, int? maxWeightGrams, decimal fee)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A fee row needs a name.", nameof(name));
        if (fee < 0) throw new ArgumentException("The fee cannot be negative.", nameof(fee));
        if (minWeightGrams < 0 || maxWeightGrams < 0) throw new ArgumentException("Weight limits cannot be negative.");
        if (minWeightGrams > maxWeightGrams) throw new ArgumentException("The minimum weight is above the maximum weight.");

        Name = name.Trim();
        Priority = priority;
        IsActive = isActive;
        MatchProvinces = string.IsNullOrWhiteSpace(matchProvinces)
            ? null
            : string.Join(", ", matchProvinces.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        MinWeightGrams = minWeightGrams;
        MaxWeightGrams = maxWeightGrams;
        Fee = fee;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool Matches(string province, int weightGrams)
    {
        if (!IsActive) return false;
        if (!string.IsNullOrWhiteSpace(MatchProvinces) &&
            !MatchProvinces.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(p => VnText.Same(p, province)))
            return false;
        if (MinWeightGrams.HasValue && weightGrams < MinWeightGrams) return false;
        if (MaxWeightGrams.HasValue && weightGrams > MaxWeightGrams) return false;
        return true;
    }
}
