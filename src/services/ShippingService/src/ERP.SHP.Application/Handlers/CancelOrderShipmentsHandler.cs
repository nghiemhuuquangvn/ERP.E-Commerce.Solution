using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Application.Handlers;

public class CancelOrderShipmentsCommand : IRequest<int>
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = "the order was cancelled";
}

/// <summary>
/// The order was cancelled (by hand or by the saga): a parcel the carrier has not collected is cancelled at the carrier and here; one it already has
/// is flagged so a person has it returned. Idempotent — a repeat finds nothing left to do. Returns how many shipments were cancelled.
/// </summary>
public class CancelOrderShipmentsCommandHandler : IRequestHandler<CancelOrderShipmentsCommand, int>
{
    private readonly IShippingUnitOfWork _uow;
    private readonly CarrierTrackingService _tracking;
    private readonly ILogger<CancelOrderShipmentsCommandHandler> _logger;

    public CancelOrderShipmentsCommandHandler(IShippingUnitOfWork uow, CarrierTrackingService tracking, ILogger<CancelOrderShipmentsCommandHandler> logger)
    {
        _uow = uow;
        _tracking = tracking;
        _logger = logger;
    }

    public async Task<int> Handle(CancelOrderShipmentsCommand request, CancellationToken ct)
    {
        var cancelled = 0;
        foreach (var shipment in await _uow.Shipments.GetTrackedByOrderIdAsync(request.OrderId, ct))
        {
            if (shipment.Status is ShipmentStatus.Cancelled or ShipmentStatus.Delivered or ShipmentStatus.Returned)
                continue;

            if (shipment.Status is ShipmentStatus.Pending or ShipmentStatus.ReadyForPickup)
            {
                var (done, problem) = await _tracking.CancelAtCarrierAsync(shipment, request.Reason, ct);
                if (done) cancelled++;
                else _logger.LogWarning("Order {OrderId}: shipment {Shipment} was not cancelled at the carrier: {Problem}", request.OrderId, shipment.Id, problem);
            }
            else
            {
                shipment.FlagForReview($"The order was cancelled but the parcel is already {shipment.Status} with {shipment.CarrierName}: have it returned.");
                await _uow.Shipments.UpdateAsync(shipment, ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return cancelled;
    }
}
