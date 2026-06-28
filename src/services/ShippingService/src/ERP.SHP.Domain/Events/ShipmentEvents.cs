using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Events;

public record ShipmentCreatedEvent(
    Guid ShipmentId, Guid OrderId, Guid CarrierId, string CarrierName) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record TrackingUpdatedEvent(
    Guid ShipmentId, Guid OrderId, string Status, string Location, string Description) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record DeliveryConfirmedEvent(
    Guid ShipmentId, Guid OrderId, DateTime DeliveredAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
