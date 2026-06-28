using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities;

public class Carrier : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public decimal BaseRatePerKg { get; private set; }
    public string? TrackingUrlTemplate { get; private set; }

    private Carrier() { }

    public static Carrier Create(string name, string code, decimal baseRatePerKg, string? trackingUrlTemplate = null)
    {
        return new Carrier
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            IsActive = true,
            BaseRatePerKg = baseRatePerKg,
            TrackingUrlTemplate = trackingUrlTemplate,
            CreatedAt = DateTime.UtcNow
        };
    }

    public decimal CalculateRate(decimal weightKg) => BaseRatePerKg * weightKg;

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
