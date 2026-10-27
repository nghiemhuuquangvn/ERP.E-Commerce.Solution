namespace ERP.SHP.Domain.Enums;

/// <summary>
/// What a carrier says about a parcel, in one vocabulary for every carrier (each provider maps its own status ids to this).
/// <see cref="Shipment.ApplyCarrierStatus"/> turns it into the shipment's own state machine: forward only, never back.
/// </summary>
public enum CarrierShipmentStatus
{
    /// <summary>Order accepted by the carrier, not collected yet (or a status we do not model): only a tracking line.</summary>
    Accepted = 0,
    PickedUp = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Delivered = 4,
    DeliveryFailed = 5,
    /// <summary>Delivery failed for good and the parcel is on its way back to the shop: only a tracking line.</summary>
    Returning = 6,
    Returned = 7,
    Cancelled = 8,
}
