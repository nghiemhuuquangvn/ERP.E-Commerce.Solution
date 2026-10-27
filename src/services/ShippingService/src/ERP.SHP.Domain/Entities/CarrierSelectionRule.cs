using ERP.Shared.Core.Domain;
using ERP.SHP.Domain.Text;

namespace ERP.SHP.Domain.Entities;

/// <summary>What the selector knows about an order that needs a carrier.</summary>
public record ShipmentFacts(string Province, int WeightGrams, decimal OrderValue, bool IsCashOnDelivery);

/// <summary>
/// "Orders like this go with that carrier." Active rules are tried in ascending <see cref="Priority"/>; the first whose conditions all hold
/// names the carrier. Every condition is optional (empty = any). Orders no rule matches are given to the cheapest quote.
/// </summary>
public class CarrierSelectionRule : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>Comma separated province names; compared without case, diacritics or "Tỉnh / Thành phố".</summary>
    public string? MatchProvinces { get; private set; }
    public int? MinWeightGrams { get; private set; }
    public int? MaxWeightGrams { get; private set; }
    public decimal? MinOrderValue { get; private set; }
    public decimal? MaxOrderValue { get; private set; }
    /// <summary>true = only cash-on-delivery orders, false = only prepaid orders, null = both.</summary>
    public bool? CodOnly { get; private set; }
    public string CarrierCode { get; private set; } = string.Empty;

    protected CarrierSelectionRule() { }

    public static CarrierSelectionRule Create(Guid tenantId, string name, int priority, string carrierCode)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required", nameof(tenantId));
        var rule = new CarrierSelectionRule { Id = Guid.NewGuid(), TenantId = tenantId, CreatedBy = "System", CreatedAt = DateTime.UtcNow };
        rule.Update(name, priority, true, null, null, null, null, null, null, carrierCode);
        return rule;
    }

    public void Update(string name, int priority, bool isActive, string? matchProvinces, int? minWeightGrams, int? maxWeightGrams,
        decimal? minOrderValue, decimal? maxOrderValue, bool? codOnly, string carrierCode)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A rule needs a name.", nameof(name));
        var code = CarrierCodes.Normalize(carrierCode) ?? throw new ArgumentException($"Unknown carrier '{carrierCode}'.", nameof(carrierCode));
        if (minWeightGrams < 0 || maxWeightGrams < 0 || minOrderValue < 0 || maxOrderValue < 0)
            throw new ArgumentException("Limits cannot be negative.");
        if (minWeightGrams > maxWeightGrams) throw new ArgumentException("The minimum weight is above the maximum weight.");
        if (minOrderValue > maxOrderValue) throw new ArgumentException("The minimum order value is above the maximum order value.");

        Name = name.Trim();
        Priority = priority;
        IsActive = isActive;
        MatchProvinces = string.IsNullOrWhiteSpace(matchProvinces)
            ? null
            : string.Join(", ", matchProvinces.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        MinWeightGrams = minWeightGrams;
        MaxWeightGrams = maxWeightGrams;
        MinOrderValue = minOrderValue;
        MaxOrderValue = maxOrderValue;
        CodOnly = codOnly;
        CarrierCode = code;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool Matches(ShipmentFacts facts)
    {
        if (!IsActive) return false;
        if (!string.IsNullOrWhiteSpace(MatchProvinces) &&
            !MatchProvinces.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(p => VnText.Same(p, facts.Province)))
            return false;
        if (MinWeightGrams.HasValue && facts.WeightGrams < MinWeightGrams) return false;
        if (MaxWeightGrams.HasValue && facts.WeightGrams > MaxWeightGrams) return false;
        if (MinOrderValue.HasValue && facts.OrderValue < MinOrderValue) return false;
        if (MaxOrderValue.HasValue && facts.OrderValue > MaxOrderValue) return false;
        if (CodOnly.HasValue && CodOnly.Value != facts.IsCashOnDelivery) return false;
        return true;
    }
}
