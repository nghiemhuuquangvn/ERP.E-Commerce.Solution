using ERP.Shared.Core.Domain;
using ERP.SHP.Domain.Events;

namespace ERP.SHP.Domain.Entities;

public enum ReturnShipmentStatus
{
    Requested = 0,
    PickedUp = 1,
    Received = 2,
    Cancelled = 3
}

/// <summary>
/// Reverse logistics is modeled as its own aggregate rather than reusing Shipment's
/// state machine — a customer return is a distinct physical movement (customer →
/// warehouse) from the original forward shipment (warehouse → customer), with its own
/// tracking number and carrier leg.
/// </summary>
public class ReturnShipment : AggregateRoot
{
    public Guid OriginalShipmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? TrackingNumber { get; private set; }
    public ReturnShipmentStatus Status { get; private set; }
    public DateTime? PickedUpAt { get; private set; }
    public DateTime? ReceivedAt { get; private set; }

    private ReturnShipment() { }

    public static ReturnShipment Create(Guid originalShipmentId, Guid orderId, string reason)
    {
        var returnShipment = new ReturnShipment
        {
            Id = Guid.NewGuid(),
            OriginalShipmentId = originalShipmentId,
            OrderId = orderId,
            Reason = reason,
            Status = ReturnShipmentStatus.Requested,
            CreatedAt = DateTime.UtcNow
        };

        returnShipment.AddDomainEvent(new ReturnInitiatedEvent(returnShipment.Id, originalShipmentId, orderId, reason));
        return returnShipment;
    }

    public void AssignTrackingNumber(string trackingNumber)
    {
        TrackingNumber = trackingNumber;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPickedUp()
    {
        if (Status != ReturnShipmentStatus.Requested)
            throw new InvalidOperationException($"Cannot mark picked up a return in {Status} state.");

        Status = ReturnShipmentStatus.PickedUp;
        PickedUpAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReceived()
    {
        if (Status != ReturnShipmentStatus.PickedUp && Status != ReturnShipmentStatus.Requested)
            throw new InvalidOperationException($"Cannot mark received a return in {Status} state.");

        Status = ReturnShipmentStatus.Received;
        ReceivedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ReturnReceivedEvent(Id, OriginalShipmentId, OrderId));
    }

    public void Cancel()
    {
        if (Status == ReturnShipmentStatus.Received)
            throw new InvalidOperationException("Cannot cancel a return that has already been received.");

        Status = ReturnShipmentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}
