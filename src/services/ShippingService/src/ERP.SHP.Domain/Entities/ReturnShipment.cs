using ERP.Shared.Core.Domain;
using ERP.SHP.Domain.Enums;
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

    // P1-11d: the reverse order booked at a carrier (customer → shop).
    public Guid? CarrierId { get; private set; }
    public string? CarrierName { get; private set; }
    public string? CarrierOrderCode { get; private set; }
    /// <summary>What the carrier charges the shop for fetching the goods.</summary>
    public decimal? CarrierFee { get; private set; }
    /// <summary>Why no carrier took the return (every carrier tried and its error); empty once one accepted it.</summary>
    public string? BookingError { get; private set; }
    public DateTime? LastCarrierSyncAt { get; private set; }

    private ReturnShipment() { }

    /// <summary>A carrier accepted the reverse order: its codes and fee.</summary>
    public void RecordBooking(Guid carrierId, string carrierName, string trackingNumber, string? carrierOrderCode, decimal? fee)
    {
        CarrierId = carrierId;
        CarrierName = carrierName;
        TrackingNumber = trackingNumber;
        CarrierOrderCode = carrierOrderCode;
        CarrierFee = fee;
        BookingError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>No carrier took the return: kept so a person sees why (and can retry or receive the goods by hand).</summary>
    public void RecordBookingFailure(string error)
    {
        BookingError = error.Length > 1000 ? error[..1000] : error;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCarrierSynced() => LastCarrierSyncAt = DateTime.UtcNow;

    /// <summary>
    /// A carrier's report on the reverse parcel: collected from the customer = picked up, delivered to the shop = received (which tells Sales the goods are back).
    /// Final states never change; a failed pickup or anything else is only noted by the carrier's own tracking.
    /// </summary>
    public void ApplyCarrierStatus(CarrierShipmentStatus status)
    {
        if (Status is ReturnShipmentStatus.Received or ReturnShipmentStatus.Cancelled) return;
        switch (status)
        {
            case CarrierShipmentStatus.PickedUp:
            case CarrierShipmentStatus.InTransit:
            case CarrierShipmentStatus.OutForDelivery:
                if (Status == ReturnShipmentStatus.Requested) MarkPickedUp();
                break;
            case CarrierShipmentStatus.Delivered:
                MarkReceived();
                break;
            case CarrierShipmentStatus.Cancelled:
                Cancel();
                break;
        }
    }

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
