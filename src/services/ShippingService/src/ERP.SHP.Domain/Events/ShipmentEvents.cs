using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Events;

public record ShipmentCreatedEvent(
    Guid ShipmentId, Guid OrderId, Guid CarrierId, string CarrierName) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record TrackingUpdatedEvent(
    Guid ShipmentId, Guid OrderId, string Status, string Location, string Description) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record DeliveryConfirmedEvent(
    Guid ShipmentId, Guid OrderId, DateTime DeliveredAt) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record ShipmentFailedEvent(
    Guid ShipmentId, Guid OrderId, string Reason, int DeliveryAttempts) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record ReturnInitiatedEvent(
    Guid ReturnShipmentId, Guid OriginalShipmentId, Guid OrderId, string Reason) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record ReturnReceivedEvent(
    Guid ReturnShipmentId, Guid OriginalShipmentId, Guid OrderId) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
