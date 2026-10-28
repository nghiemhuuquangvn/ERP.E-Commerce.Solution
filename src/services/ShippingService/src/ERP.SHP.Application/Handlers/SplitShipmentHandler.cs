using ERP.Shared.Infrastructure.Exceptions;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Application.Handlers;

public class SplitShipmentCommand : IRequest<List<ShipmentDto>>
{
    public Guid ShipmentId { get; set; }
    public List<ParcelSplitPart> Parts { get; set; } = new();
}

/// <summary>
/// A person splits a parcel the carrier has not collected into several (ROADMAP P1-11d). The new parcels are booked FIRST, then the old booking is cancelled:
/// if any carrier refuses a new parcel, or refuses to cancel the old one, the new bookings are undone and nothing changes (the order is never left without a booking).
/// </summary>
public class SplitShipmentCommandHandler : IRequestHandler<SplitShipmentCommand, List<ShipmentDto>>
{
    private readonly IShippingUnitOfWork _uow;
    private readonly CarrierBookingService _booking;
    private readonly CarrierTrackingService _tracking;
    private readonly ILogger<SplitShipmentCommandHandler> _logger;

    public SplitShipmentCommandHandler(IShippingUnitOfWork uow, CarrierBookingService booking, CarrierTrackingService tracking, ILogger<SplitShipmentCommandHandler> logger)
    {
        _uow = uow;
        _booking = booking;
        _tracking = tracking;
        _logger = logger;
    }

    public async Task<List<ShipmentDto>> Handle(SplitShipmentCommand request, CancellationToken ct)
    {
        var old = await _uow.Shipments.GetByIdAsync(request.ShipmentId, ct)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");
        if (old.Status is not (ShipmentStatus.Pending or ShipmentStatus.ReadyForPickup))
            throw new BusinessRuleException("Shipping.Parcels", $"a parcel that is {old.Status} cannot be split: only a parcel the carrier has not collected");

        // The order's other live parcels: a split order is not split again behind the first split's back (numbers would clash).
        var siblings = (await _uow.Shipments.GetTrackedByOrderIdAsync(old.OrderId, ct)).Where(s => s.Id != old.Id && s.Status != ShipmentStatus.Cancelled).ToList();
        var firstNo = siblings.Count == 0 ? 1 : siblings.Max(s => s.ParcelNo) + 1;
        var plan = ParcelSplitPlanner.Plan(old, request.Parts);
        var total = siblings.Count + plan.Count;

        var sender = await _booking.SenderAsync(ct);
        var recipient = new ShipmentRecipient(old.RecipientName, old.RecipientPhone, old.AddressStreet, old.AddressWard, old.AddressDistrict, old.AddressProvince, old.AddressPostalCode, old.AddressCountry);

        // The carrier is chosen per parcel by the shop's rules, then the cheapest quote (a person may have picked another for the original).
        var created = new List<Shipment>();
        try
        {
            foreach (var p in plan)
            {
                var shipment = Shipment.Create(old.OrderId, Guid.Empty, "(booking)", recipient, p.Items, old.EstimatedDelivery);
                shipment.SetParcel(firstNo + p.ParcelNo - 1, total);
                shipment.SetCommercials(p.CodAmount, p.DeclaredValue);
                var parcel = p.Items.Select(i => new CarrierParcelItem(i.Name, i.Quantity, 0m, (int)Math.Round(i.Weight * 1000m))).ToList();
                await _booking.BookAsync(shipment, sender.Address, parcel, new BookingOrder(shipment.BookingReference, $"Order {old.OrderId:N} parcel {shipment.ParcelNo}"), null, ct);
                created.Add(shipment);
            }

            var (cancelled, problem) = await _tracking.CancelAtCarrierAsync(old, "split into several parcels", ct);
            if (!cancelled)
                throw new BusinessRuleException("Shipping.Parcels", $"the carrier did not cancel the original parcel, so it was not split: {problem}");
        }
        catch
        {
            foreach (var s in created.Where(s => !string.IsNullOrEmpty(s.TrackingNumber)))
            {
                var (ok, why) = await _tracking.CancelBookingAsync(s.CarrierId, s.TrackingNumber!, "split undone", CancellationToken.None);
                if (!ok) _logger.LogError("Split of {Shipment} failed and booking {Tracking} could not be undone at the carrier: {Why}", old.Id, s.TrackingNumber, why);
            }
            throw;
        }

        // Every live parcel of the order now knows how many parcels the order has.
        foreach (var s in siblings) { s.SetParcel(s.ParcelNo, total); await _uow.Shipments.UpdateAsync(s, ct); }
        foreach (var s in created) s.FinalizeCreation();
        foreach (var s in created) await _uow.Shipments.AddAsync(s, ct);
        await _uow.SaveChangesAsync(ct);
        return created.Select(CreateShipmentCommandHandler.MapToDto).ToList();
    }
}
