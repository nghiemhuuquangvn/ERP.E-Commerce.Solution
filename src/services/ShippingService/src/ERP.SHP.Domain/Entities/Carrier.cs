using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities;

public class Carrier : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public decimal BaseRatePerKg { get; private set; }
    public decimal InterProvinceSurcharge { get; private set; }
    public string? OriginProvince { get; private set; }
    public string? TrackingUrlTemplate { get; private set; }

    private Carrier() { }

    public static Carrier Create(
        string name, string code, decimal baseRatePerKg, string? trackingUrlTemplate = null,
        decimal interProvinceSurcharge = 0, string? originProvince = null)
    {
        return new Carrier
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            IsActive = true,
            BaseRatePerKg = baseRatePerKg,
            InterProvinceSurcharge = interProvinceSurcharge,
            OriginProvince = originProvince,
            TrackingUrlTemplate = trackingUrlTemplate,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Distance is approximated by same-province vs. cross-province rather than real
    /// geocoded distance (no geocoding/mapping integration exists in this service) —
    /// this is the standard simplified model Vietnamese carriers themselves use for
    /// a first-pass quote (nội tỉnh vs liên tỉnh).
    /// </summary>
    public decimal CalculateRate(decimal weightKg, string? destinationProvince = null)
    {
        var rate = BaseRatePerKg * weightKg;

        var isInterProvince = !string.IsNullOrWhiteSpace(OriginProvince)
            && !string.IsNullOrWhiteSpace(destinationProvince)
            && !string.Equals(OriginProvince, destinationProvince, StringComparison.OrdinalIgnoreCase);

        if (isInterProvince)
            rate += InterProvinceSurcharge;

        return rate;
    }

    public DateTime EstimateDeliveryDate(string? destinationProvince = null)
    {
        var isInterProvince = !string.IsNullOrWhiteSpace(OriginProvince)
            && !string.IsNullOrWhiteSpace(destinationProvince)
            && !string.Equals(OriginProvince, destinationProvince, StringComparison.OrdinalIgnoreCase);

        return DateTime.UtcNow.AddDays(isInterProvince ? 4 : 2);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
