using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;

namespace ERP.SHP.Application.Services;

/// <summary>An order is delivered when ALL its parcels are (ROADMAP P1-11d): before a parcel is marked delivered it is told how many of the order's other parcels are still on the way.</summary>
public static class ParcelProgress
{
    /// <summary>The same for the way back: the goods of a return are home when no other reverse parcel of the order is still on its way.</summary>
    public static async Task TellOthersPendingAsync(IShippingUnitOfWork uow, ReturnShipment returned, CancellationToken ct = default)
    {
        var all = await uow.ReturnShipments.GetTrackedByOrderIdAsync(returned.OrderId, ct);
        returned.SetOtherReturnsPending(all.Count(r => r.Id != returned.Id && r.Status is not (ReturnShipmentStatus.Received or ReturnShipmentStatus.Cancelled)));
    }

    public static async Task TellSiblingsPendingAsync(IShippingUnitOfWork uow, Shipment shipment, CancellationToken ct = default)
    {
        if (shipment.ParcelCount <= 1) return;
        var others = await uow.Shipments.GetByOrderIdAsync(shipment.OrderId, ct);
        shipment.SetOtherParcelsPending(others.Count(s => s.Id != shipment.Id && s.Status is not (ShipmentStatus.Delivered or ShipmentStatus.Cancelled)));
    }
}
