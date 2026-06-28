using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities;

public class TrackingEvent : BaseEntity
{
    public Guid ShipmentId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    private TrackingEvent() { }

    public static TrackingEvent Create(Guid shipmentId, string status, string location, string description)
    {
        return new TrackingEvent
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            Status = status,
            Location = location,
            Description = description,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}
