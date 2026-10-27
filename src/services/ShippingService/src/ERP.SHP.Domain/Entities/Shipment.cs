using ERP.Shared.Core.Domain;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Domain.Events;

namespace ERP.SHP.Domain.Entities;

public class Shipment : AggregateRoot
{
    private readonly List<ShipmentItem> _items = new();
    private readonly List<TrackingEvent> _trackingEvents = new();

    public Guid OrderId { get; private set; }
    public Guid CarrierId { get; private set; }
    public string CarrierName { get; private set; } = string.Empty;
    public string? TrackingNumber { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string RecipientName { get; private set; } = string.Empty;
    public string? RecipientPhone { get; private set; }
    public string AddressStreet { get; private set; } = string.Empty;
    public string? AddressWard { get; private set; }
    public string AddressDistrict { get; private set; } = string.Empty;
    public string AddressProvince { get; private set; } = string.Empty;
    public string? AddressPostalCode { get; private set; }
    public string AddressCountry { get; private set; } = "Vietnam";
    public DateTime? EstimatedDelivery { get; private set; }
    public DateTime? PickedUpAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public int DeliveryAttempts { get; private set; }
    public string? ReceivedByName { get; private set; }
    public string? SignatureUrl { get; private set; }
    public string? PhotoUrl { get; private set; }
    public decimal TotalWeight { get; private set; }

    // P1-11: what the carrier was asked and what it answered.
    /// <summary>Cash the carrier collects from the recipient (0 when the order was paid online).</summary>
    public decimal CodAmount { get; private set; }
    /// <summary>Value declared to the carrier for insurance (the goods' value).</summary>
    public decimal DeclaredValue { get; private set; }
    /// <summary>The fee the carrier charges the shop (not the customer: the order has no shipping fee yet).</summary>
    public decimal? CarrierFee { get; private set; }
    /// <summary>The carrier's own order id when it differs from the tracking code.</summary>
    public string? CarrierOrderCode { get; private set; }
    /// <summary>Why this carrier: the rule that matched or the quotes compared.</summary>
    public string? SelectionNote { get; private set; }
    /// <summary>Why the booking failed (carriers tried and their errors); empty once a carrier accepted it.</summary>
    public string? BookingError { get; private set; }
    public DateTime? LastCarrierSyncAt { get; private set; }
    /// <summary>Something a person should look at (e.g. the carrier collected another cash amount than expected).</summary>
    public string? ReviewNote { get; private set; }
    public IReadOnlyCollection<ShipmentItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TrackingEvent> TrackingEvents => _trackingEvents.AsReadOnly();

    private Shipment() { }

    public static Shipment Create(Guid orderId, Guid carrierId, string carrierName,
        ShipmentRecipient recipient, List<(Guid productId, string name, int qty, decimal weight)> items,
        DateTime? estimatedDelivery = null)
    {
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CarrierId = carrierId,
            CarrierName = carrierName,
            RecipientName = recipient.RecipientName,
            RecipientPhone = recipient.RecipientPhone,
            AddressStreet = recipient.AddressStreet,
            AddressWard = recipient.AddressWard,
            AddressDistrict = recipient.AddressDistrict,
            AddressProvince = recipient.AddressProvince,
            AddressPostalCode = recipient.AddressPostalCode,
            AddressCountry = string.IsNullOrWhiteSpace(recipient.AddressCountry) ? "Vietnam" : recipient.AddressCountry,
            EstimatedDelivery = estimatedDelivery,
            Status = ShipmentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (productId, name, qty, weight) in items)
            shipment._items.Add(ShipmentItem.Create(shipment.Id, productId, name, qty, weight));

        shipment.TotalWeight = shipment._items.Sum(i => i.Weight * i.Quantity);
        shipment.AddDomainEvent(new ShipmentCreatedEvent(shipment.Id, orderId, carrierId, carrierName));
        return shipment;
    }

    public void AssignTrackingNumber(string trackingNumber)
    {
        TrackingNumber = trackingNumber;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The carrier is known now: announce the shipment with the carrier that accepted it (Create raised the event with the first candidate,
    /// which may not be the one that booked it).
    /// </summary>
    public void FinalizeCreation()
    {
        ClearDomainEvents();
        AddDomainEvent(new ShipmentCreatedEvent(Id, OrderId, CarrierId, CarrierName));
    }

    public void SetCommercials(decimal codAmount, decimal declaredValue)
    {
        CodAmount = Math.Max(0m, Math.Round(codAmount, 2));
        DeclaredValue = Math.Max(0m, Math.Round(declaredValue, 2));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>A carrier accepted the parcel: its codes, fee and the reason it was chosen.</summary>
    public void RecordBooking(Guid carrierId, string carrierName, string trackingNumber, string? carrierOrderCode, decimal? fee, DateTime? estimatedDelivery, string? selectionNote)
    {
        CarrierId = carrierId;
        CarrierName = carrierName;
        TrackingNumber = trackingNumber;
        CarrierOrderCode = carrierOrderCode;
        CarrierFee = fee;
        if (estimatedDelivery.HasValue) EstimatedDelivery = estimatedDelivery;
        SelectionNote = selectionNote;
        BookingError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Every carrier tried refused (or none was usable): kept on the shipment so a person sees why.</summary>
    public void RecordBookingFailure(string error)
    {
        BookingError = error.Length > 1000 ? error[..1000] : error;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCarrierSynced()
    {
        LastCarrierSyncAt = DateTime.UtcNow;
    }

    public void FlagForReview(string note)
    {
        ReviewNote = note.Length > 500 ? note[..500] : note;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// A carrier's report applied to the shipment: the tracking line is always added, the state only moves forward and never leaves a final
    /// state (Delivered, Returned, Cancelled). A carrier may skip steps (picked up and delivered in one message), so intermediate states are passed through.
    /// </summary>
    public void ApplyCarrierStatus(CarrierShipmentStatus status, string location, string description)
    {
        if (Status is ShipmentStatus.Delivered or ShipmentStatus.Returned or ShipmentStatus.Cancelled)
            return;

        switch (status)
        {
            case CarrierShipmentStatus.Accepted:
            case CarrierShipmentStatus.Returning:
                AddTrackingEvent(status.ToString(), location, description);
                break;
            case CarrierShipmentStatus.PickedUp:
                if (Status is ShipmentStatus.Pending or ShipmentStatus.ReadyForPickup) MarkAsPickedUp();
                else AddTrackingEvent("PickedUp", location, description);
                break;
            case CarrierShipmentStatus.InTransit:
                if (Status is ShipmentStatus.Pending or ShipmentStatus.ReadyForPickup) MarkAsPickedUp();
                if (Status is ShipmentStatus.PickedUp or ShipmentStatus.DeliveryFailed) MarkInTransit(location);
                else AddTrackingEvent("InTransit", location, description);
                break;
            case CarrierShipmentStatus.OutForDelivery:
                if (Status is ShipmentStatus.Pending or ShipmentStatus.ReadyForPickup) MarkAsPickedUp();
                if (Status != ShipmentStatus.OutForDelivery) MarkOutForDelivery(location);
                else AddTrackingEvent("OutForDelivery", location, description);
                break;
            case CarrierShipmentStatus.Delivered:
                if (Status is ShipmentStatus.Pending or ShipmentStatus.ReadyForPickup) MarkAsPickedUp();
                if (Status is not (ShipmentStatus.OutForDelivery or ShipmentStatus.InTransit)) MarkOutForDelivery(location);
                MarkAsDelivered(location);
                break;
            case CarrierShipmentStatus.DeliveryFailed:
                MarkDeliveryFailed(string.IsNullOrWhiteSpace(description) ? "carrier reported a failed delivery" : description);
                break;
            case CarrierShipmentStatus.Returned:
                MarkReturned(description);
                break;
            case CarrierShipmentStatus.Cancelled:
                Cancel(string.IsNullOrWhiteSpace(description) ? "cancelled at the carrier" : description);
                AddTrackingEvent("Cancelled", location, description);
                break;
        }
    }

    public void MarkReturned(string? note)
    {
        if (Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled) return;
        Status = ShipmentStatus.Returned;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("Returned", CarrierName, string.IsNullOrWhiteSpace(note) ? "Parcel returned to the sender" : note);
    }

    public void MarkReadyForPickup()
    {
        if (Status != ShipmentStatus.Pending)
            return; // already progressed (or arrived out of order) — not an error, just a no-op

        Status = ShipmentStatus.ReadyForPickup;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("ReadyForPickup", "Warehouse", "Inventory reserved — ready for carrier pickup");
    }

    public void AddTrackingEvent(string status, string location, string description)
    {
        var trackingEvent = TrackingEvent.Create(Id, status, location, description);
        _trackingEvents.Add(trackingEvent);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new TrackingUpdatedEvent(Id, OrderId, status, location, description));
    }

    public void MarkAsPickedUp()
    {
        if (Status != ShipmentStatus.Pending && Status != ShipmentStatus.ReadyForPickup)
            throw new InvalidOperationException($"Cannot pick up shipment in {Status} state.");

        Status = ShipmentStatus.PickedUp;
        PickedUpAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("PickedUp", CarrierName, "Package picked up by carrier");
    }

    public void MarkInTransit(string location)
    {
        Status = ShipmentStatus.InTransit;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("InTransit", location, "Package in transit");
    }

    public void MarkOutForDelivery(string location)
    {
        Status = ShipmentStatus.OutForDelivery;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("OutForDelivery", location, "Package out for delivery");
    }

    public void MarkAsDelivered(string location, string? receivedByName = null, string? signatureUrl = null, string? photoUrl = null)
    {
        if (Status != ShipmentStatus.OutForDelivery && Status != ShipmentStatus.InTransit)
            throw new InvalidOperationException($"Cannot deliver shipment in {Status} state.");

        Status = ShipmentStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        ReceivedByName = receivedByName;
        SignatureUrl = signatureUrl;
        PhotoUrl = photoUrl;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("Delivered", location, "Package delivered successfully");
        AddDomainEvent(new DeliveryConfirmedEvent(Id, OrderId, DeliveredAt.Value));
    }

    public void MarkDeliveryFailed(string reason)
    {
        if (Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled or ShipmentStatus.Returned)
            throw new InvalidOperationException($"Cannot fail delivery for a shipment in {Status} state.");

        Status = ShipmentStatus.DeliveryFailed;
        DeliveryAttempts++;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("DeliveryFailed", $"{AddressDistrict}, {AddressProvince}", $"Delivery attempt failed: {reason}");
        AddDomainEvent(new ShipmentFailedEvent(Id, OrderId, reason, DeliveryAttempts));
    }

    public void RetryDelivery()
    {
        if (Status != ShipmentStatus.DeliveryFailed)
            throw new InvalidOperationException($"Cannot retry delivery for a shipment in {Status} state.");

        Status = ShipmentStatus.OutForDelivery;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("OutForDelivery", $"{AddressDistrict}, {AddressProvince}", $"Delivery re-attempt #{DeliveryAttempts + 1}");
    }

    public void Cancel(string reason)
    {
        if (Status is ShipmentStatus.Delivered or ShipmentStatus.Returned)
            throw new InvalidOperationException($"Cannot cancel shipment in {Status} state.");

        Status = ShipmentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancelReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }
}

public record ShipmentRecipient(
    string RecipientName,
    string? RecipientPhone,
    string AddressStreet,
    string? AddressWard,
    string AddressDistrict,
    string AddressProvince,
    string? AddressPostalCode,
    string AddressCountry = "Vietnam");
