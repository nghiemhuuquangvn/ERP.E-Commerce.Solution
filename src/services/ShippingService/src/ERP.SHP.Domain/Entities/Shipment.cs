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
    public string ShippingAddress { get; private set; } = string.Empty;
    public DateTime? EstimatedDelivery { get; private set; }
    public DateTime? PickedUpAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public decimal TotalWeight { get; private set; }
    public IReadOnlyCollection<ShipmentItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TrackingEvent> TrackingEvents => _trackingEvents.AsReadOnly();

    private Shipment() { }

    public static Shipment Create(Guid orderId, Guid carrierId, string carrierName,
        string shippingAddress, List<(Guid productId, string name, int qty, decimal weight)> items,
        DateTime? estimatedDelivery = null)
    {
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CarrierId = carrierId,
            CarrierName = carrierName,
            ShippingAddress = shippingAddress,
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

    public void AddTrackingEvent(string status, string location, string description)
    {
        var trackingEvent = TrackingEvent.Create(Id, status, location, description);
        _trackingEvents.Add(trackingEvent);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new TrackingUpdatedEvent(Id, OrderId, status, location, description));
    }

    public void MarkAsPickedUp()
    {
        if (Status != ShipmentStatus.Pending)
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

    public void MarkAsDelivered(string location)
    {
        if (Status != ShipmentStatus.OutForDelivery && Status != ShipmentStatus.InTransit)
            throw new InvalidOperationException($"Cannot deliver shipment in {Status} state.");

        Status = ShipmentStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("Delivered", location, "Package delivered successfully");
        AddDomainEvent(new DeliveryConfirmedEvent(Id, OrderId, DeliveredAt.Value));
    }

    public void MarkDeliveryFailed(string reason)
    {
        Status = ShipmentStatus.DeliveryFailed;
        UpdatedAt = DateTime.UtcNow;
        AddTrackingEvent("DeliveryFailed", ShippingAddress, $"Delivery attempt failed: {reason}");
    }

    public void Cancel(string reason)
    {
        if (Status is ShipmentStatus.Delivered or ShipmentStatus.Returned)
            throw new InvalidOperationException($"Cannot cancel shipment in {Status} state.");

        Status = ShipmentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}
