using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Application.Services;

/// <summary>
/// What a carrier says about a parcel, applied to the shipment — the ONE place a carrier's report changes a shipment, whether it came as a webhook
/// or from the polling job. And what a shop does to a parcel at the carrier (cancel it).
/// </summary>
public class CarrierTrackingService
{
    private readonly IShippingUnitOfWork _uow;
    private readonly ICarrierProviderResolver _carriers;
    private readonly ILogger<CarrierTrackingService> _logger;

    public CarrierTrackingService(IShippingUnitOfWork uow, ICarrierProviderResolver carriers, ILogger<CarrierTrackingService> logger)
    {
        _uow = uow;
        _carriers = carriers;
        _logger = logger;
    }

    /// <summary>
    /// Applies a carrier status. Delivery raises <c>DeliveryConfirmed</c>, which makes Payment capture a cash-on-delivery payment, so when the carrier
    /// names the cash it collected and that is not what was expected the shipment is flagged for a person (the payment is still captured at the order's
    /// amount: reconciling cash with the carrier is a finance task, see DEFERRED).
    /// </summary>
    public async Task ApplyAsync(Shipment shipment, CarrierShipmentStatus status, string carrierStatus, string? reason, decimal? codCollected, CancellationToken ct = default)
    {
        var description = string.IsNullOrWhiteSpace(reason) ? $"Carrier status {carrierStatus}" : $"{reason} (carrier status {carrierStatus})";
        shipment.ApplyCarrierStatus(status, shipment.CarrierName, description);

        if (status == CarrierShipmentStatus.Delivered && codCollected.HasValue && codCollected.Value != shipment.CodAmount)
        {
            _logger.LogWarning("Shipment {Shipment}: the carrier collected {Collected} but {Expected} was expected", shipment.Id, codCollected, shipment.CodAmount);
            shipment.FlagForReview($"The carrier reports collecting {codCollected:N0} VND on delivery; the order expected {shipment.CodAmount:N0} VND.");
        }

        shipment.MarkCarrierSynced();
        await _uow.Shipments.UpdateAsync(shipment, ct);
    }

    /// <summary>The carrier code of a shipment (its <see cref="Carrier"/> row), or null for a shipment that has no integrated carrier.</summary>
    public async Task<string?> CarrierCodeOfAsync(Shipment shipment, CancellationToken ct = default)
    {
        var row = await _uow.Carriers.GetByIdAsync(shipment.CarrierId, ct);
        return CarrierCodes.Normalize(row?.Code);
    }

    /// <summary>
    /// Cancels the parcel at its carrier and then the shipment. Only a parcel the carrier has not collected can be cancelled; the carrier's refusal
    /// is returned (and kept as a review note), never hidden. A shipment without a tracking number was never booked: it is just cancelled.
    /// </summary>
    public async Task<(bool Cancelled, string? Problem)> CancelAtCarrierAsync(Shipment shipment, string reason, CancellationToken ct = default)
    {
        if (shipment.Status is ShipmentStatus.Cancelled)
            return (true, null);
        if (shipment.Status is not (ShipmentStatus.Pending or ShipmentStatus.ReadyForPickup))
            return (false, $"the parcel is already {shipment.Status}: the carrier no longer cancels it");

        if (!string.IsNullOrEmpty(shipment.TrackingNumber))
        {
            var code = await CarrierCodeOfAsync(shipment, ct);
            if (code == null) return (false, "this shipment's carrier has no integration, cancel the parcel with the carrier by hand");

            CarrierCancelResult result;
            try
            {
                var resolved = await _carriers.ResolveAsync(code, ct);
                result = await resolved.Provider.CancelAsync(resolved.Credentials, shipment.TrackingNumber, reason, ct);
            }
            catch (Exception ex) when (ex is ERP.Shared.Infrastructure.Exceptions.BusinessRuleException)
            {
                result = new CarrierCancelResult(false, ex.Message);
            }

            if (!result.Success)
            {
                shipment.FlagForReview($"The carrier did not cancel the parcel: {result.Error}");
                await _uow.Shipments.UpdateAsync(shipment, ct);
                return (false, result.Error);
            }
        }

        shipment.Cancel(reason);
        await _uow.Shipments.UpdateAsync(shipment, ct);
        return (true, null);
    }
}
