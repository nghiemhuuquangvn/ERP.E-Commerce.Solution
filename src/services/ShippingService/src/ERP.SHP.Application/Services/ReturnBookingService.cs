using ERP.Shared.Infrastructure.Exceptions;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Application.Services;

/// <summary>
/// Books the way back of a returned order at a carrier (ROADMAP P1-11d): one reverse order per delivered parcel, customer → the shop's pickup address, no cash to
/// collect. It is the forward booking turned round (<see cref="CarrierBookingService.BookAsync"/> with the two addresses swapped), so the shop's carrier rules, the
/// quotes and every provider work unchanged and nothing new is asked of a carrier. A refusal is kept on the <see cref="ReturnShipment"/> (the order stays waiting,
/// the goods can be received by hand); asking again books only what is not booked yet.
/// </summary>
public class ReturnBookingService
{
    private readonly IShippingUnitOfWork _uow;
    private readonly CarrierBookingService _booking;
    private readonly ILogger<ReturnBookingService> _logger;

    public ReturnBookingService(IShippingUnitOfWork uow, CarrierBookingService booking, ILogger<ReturnBookingService> logger)
    {
        _uow = uow;
        _booking = booking;
        _logger = logger;
    }

    public async Task<List<ReturnShipment>> BookReturnsAsync(Guid orderId, string reason, CancellationToken ct = default)
    {
        var parcels = (await _uow.Shipments.GetTrackedByOrderIdAsync(orderId, ct))
            .Where(s => s.Status == ShipmentStatus.Delivered).OrderBy(s => s.ParcelNo).ToList();
        if (parcels.Count == 0)
            throw new BusinessRuleException("Shipping.Returns", "the order has no delivered parcel to fetch back");

        var sender = await _booking.SenderAsync(ct);   // the shop's pickup address is where the goods go back to
        var existing = await _uow.ReturnShipments.GetTrackedByOrderIdAsync(orderId, ct);
        var result = new List<ReturnShipment>();

        foreach (var parcel in parcels)
        {
            var returned = existing.FirstOrDefault(r => r.OriginalShipmentId == parcel.Id && r.Status != ReturnShipmentStatus.Cancelled);
            if (returned != null && !string.IsNullOrEmpty(returned.TrackingNumber))
            {
                result.Add(returned);   // already booked (a second approve is not a second booking)
                continue;
            }
            if (returned == null)
            {
                returned = ReturnShipment.Create(parcel.Id, orderId, reason);
                await _uow.ReturnShipments.AddAsync(returned, ct);
            }

            // The forward booking turned round: the customer is the sender, the shop's pickup address the recipient, no cash.
            var back = new ShipmentRecipient(sender.Address.Name, sender.Address.Phone, sender.Address.Street, sender.Address.Ward, sender.Address.District, sender.Address.Province, null);
            var items = parcel.Items.Select(i => (i.ProductId, i.ProductName, i.Quantity, i.Weight)).ToList();
            var vehicle = Shipment.Create(orderId, Guid.Empty, "(booking)", back, items);   // never saved: it only carries the booking's answer
            vehicle.SetCommercials(0m, parcel.DeclaredValue);
            var customer = new CarrierAddress(parcel.RecipientName, parcel.RecipientPhone ?? string.Empty, parcel.AddressStreet, parcel.AddressWard, parcel.AddressDistrict, parcel.AddressProvince);
            var lines = parcel.Items.Select(i => new CarrierParcelItem(i.ProductName, i.Quantity, 0m, (int)Math.Round((i.Weight > 0 ? i.Weight : sender.DefaultItemWeightGrams / 1000m) * 1000m))).ToList();

            try
            {
                await _booking.BookAsync(vehicle, customer, lines, new BookingOrder($"RT-{orderId:N}-P{parcel.ParcelNo}", $"Return of order {orderId:N}"), null, ct);
                returned.RecordBooking(vehicle.CarrierId, vehicle.CarrierName, vehicle.TrackingNumber!, vehicle.CarrierOrderCode, vehicle.CarrierFee);
            }
            catch (CarrierBookingException ex)
            {
                _logger.LogWarning("Return of order {Order} parcel {Parcel} not booked: {Reason}", orderId, parcel.ParcelNo, ex.Message);
                returned.RecordBookingFailure(ex.Message);
            }
            result.Add(returned);
        }

        await _uow.SaveChangesAsync(ct);
        return result;
    }
}
