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
