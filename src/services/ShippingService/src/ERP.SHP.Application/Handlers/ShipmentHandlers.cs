using ERP.SHP.Application.Commands;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Queries;
using ERP.SHP.Domain.Entities;
using MediatR;

namespace ERP.SHP.Application.Handlers;

public class CreateShipmentCommandHandler : IRequestHandler<CreateShipmentCommand, ShipmentDto>
{
    private readonly IShipmentRepository _shipments;
    private readonly ICarrierRepository _carriers;
    private readonly ICarrierClient _carrierClient;

    public CreateShipmentCommandHandler(IShipmentRepository shipments, ICarrierRepository carriers, ICarrierClient carrierClient)
    {
        _shipments = shipments;
        _carriers = carriers;
        _carrierClient = carrierClient;
    }

    public async Task<ShipmentDto> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var carrier = await _carriers.GetByIdAsync(request.CarrierId, cancellationToken)
            ?? throw new InvalidOperationException($"Carrier {request.CarrierId} not found.");

        var items = request.Items
            .Select(i => (i.ProductId, i.ProductName, i.Quantity, i.Weight))
            .ToList();

        var r = request.Recipient;
        var recipient = new ShipmentRecipient(
            r.RecipientName, r.RecipientPhone, r.AddressStreet, r.AddressWard,
            r.AddressDistrict, r.AddressProvince, r.AddressPostalCode, r.AddressCountry);

        var estimatedDelivery = request.EstimatedDelivery ?? carrier.EstimateDeliveryDate(r.AddressProvince);

        var shipment = Shipment.Create(request.OrderId, request.CarrierId, carrier.Name, recipient, items, estimatedDelivery);

        // Previously nothing ever called the carrier when a shipment was created —
        // ICarrierClient (GHN, real) existed but only AssignTrackingNumberCommand
        // (a separate, manual step) ever set a tracking number. Booking the carrier
        // order here is what makes "real carrier integration" actually load-bearing.
        var carrierResult = await _carrierClient.CreateOrderAsync(new CarrierOrderRequest(
            r.RecipientName,
            r.RecipientPhone ?? string.Empty,
            r.AddressStreet,
            r.AddressWard ?? string.Empty,
            r.AddressDistrict,
            r.AddressProvince,
            shipment.TotalWeight,
            0,
            $"Shipment for order {request.OrderId}",
            request.Items.Select(i => new CarrierItem(i.ProductName, i.Quantity, 0)).ToList()
        ), cancellationToken);

        if (carrierResult.Success && !string.IsNullOrEmpty(carrierResult.TrackingCode))
            shipment.AssignTrackingNumber(carrierResult.TrackingCode);

        await _shipments.AddAsync(shipment, cancellationToken);
        return MapToDto(shipment);
    }

    internal static ShipmentDto MapToDto(Shipment s) => new()
    {
        Id = s.Id,
        OrderId = s.OrderId,
        CarrierId = s.CarrierId,
        CarrierName = s.CarrierName,
        TrackingNumber = s.TrackingNumber,
        Status = s.Status.ToString(),
        RecipientName = s.RecipientName,
        RecipientPhone = s.RecipientPhone,
        AddressStreet = s.AddressStreet,
        AddressWard = s.AddressWard,
        AddressDistrict = s.AddressDistrict,
        AddressProvince = s.AddressProvince,
        AddressPostalCode = s.AddressPostalCode,
        AddressCountry = s.AddressCountry,
        EstimatedDelivery = s.EstimatedDelivery,
        PickedUpAt = s.PickedUpAt,
        DeliveredAt = s.DeliveredAt,
        CancelledAt = s.CancelledAt,
        CancelReason = s.CancelReason,
        DeliveryAttempts = s.DeliveryAttempts,
        ReceivedByName = s.ReceivedByName,
        SignatureUrl = s.SignatureUrl,
        PhotoUrl = s.PhotoUrl,
        TotalWeight = s.TotalWeight,
        CreatedAt = s.CreatedAt,
        Items = s.Items.Select(i => new ShipmentItemDto
        {
            ProductId = i.ProductId, ProductName = i.ProductName,
            Quantity = i.Quantity, Weight = i.Weight
        }).ToList(),
        TrackingEvents = s.TrackingEvents.OrderByDescending(e => e.OccurredAt).Select(e => new TrackingEventDto
        {
            Status = e.Status, Location = e.Location,
            Description = e.Description, OccurredAt = e.OccurredAt
        }).ToList()
    };
}

public class AssignTrackingNumberCommandHandler : IRequestHandler<AssignTrackingNumberCommand, ShipmentDto>
{
    private readonly IShipmentRepository _shipments;
    public AssignTrackingNumberCommandHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<ShipmentDto> Handle(AssignTrackingNumberCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.AssignTrackingNumber(request.TrackingNumber);
        await _shipments.UpdateAsync(shipment, cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class AddTrackingEventCommandHandler : IRequestHandler<AddTrackingEventCommand, ShipmentDto>
{
    private readonly IShipmentRepository _shipments;
    public AddTrackingEventCommandHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<ShipmentDto> Handle(AddTrackingEventCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.AddTrackingEvent(request.Status, request.Location, request.Description);
        await _shipments.UpdateAsync(shipment, cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class ConfirmDeliveryCommandHandler : IRequestHandler<ConfirmDeliveryCommand, ShipmentDto>
{
    private readonly IShipmentRepository _shipments;
    public ConfirmDeliveryCommandHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<ShipmentDto> Handle(ConfirmDeliveryCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.MarkAsDelivered(request.Location, request.ReceivedByName, request.SignatureUrl, request.PhotoUrl);
        await _shipments.UpdateAsync(shipment, cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class MarkDeliveryFailedCommandHandler : IRequestHandler<MarkDeliveryFailedCommand, ShipmentDto>
{
    private readonly IShipmentRepository _shipments;
    public MarkDeliveryFailedCommandHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<ShipmentDto> Handle(MarkDeliveryFailedCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.MarkDeliveryFailed(request.Reason);
        await _shipments.UpdateAsync(shipment, cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class RetryDeliveryCommandHandler : IRequestHandler<RetryDeliveryCommand, ShipmentDto>
{
    private readonly IShipmentRepository _shipments;
    public RetryDeliveryCommandHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<ShipmentDto> Handle(RetryDeliveryCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.RetryDelivery();
        await _shipments.UpdateAsync(shipment, cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class BulkUpdateTrackingCommandHandler : IRequestHandler<BulkUpdateTrackingCommand, List<ShipmentDto>>
{
    private readonly IShipmentRepository _shipments;
    public BulkUpdateTrackingCommandHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<List<ShipmentDto>> Handle(BulkUpdateTrackingCommand request, CancellationToken cancellationToken)
    {
        var results = new List<ShipmentDto>();

        foreach (var update in request.Updates)
        {
            var shipment = await _shipments.GetByIdAsync(update.ShipmentId, cancellationToken);
            if (shipment == null)
                continue; // best-effort: bulk operation should not fail the whole batch for one bad id

            shipment.AddTrackingEvent(update.Status, update.Location, update.Description);
            await _shipments.UpdateAsync(shipment, cancellationToken);
            results.Add(CreateShipmentCommandHandler.MapToDto(shipment));
        }

        return results;
    }
}

public class GetShipmentQueryHandler : IRequestHandler<GetShipmentQuery, ShipmentDto?>
{
    private readonly IShipmentRepository _shipments;
    public GetShipmentQueryHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<ShipmentDto?> Handle(GetShipmentQuery request, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken);
        return shipment == null ? null : CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class TrackShipmentQueryHandler : IRequestHandler<TrackShipmentQuery, List<TrackingEventDto>>
{
    private readonly IShipmentRepository _shipments;
    public TrackShipmentQueryHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<List<TrackingEventDto>> Handle(TrackShipmentQuery request, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken);
        if (shipment == null) return new();

        return shipment.TrackingEvents.OrderByDescending(e => e.OccurredAt)
            .Select(e => new TrackingEventDto
            {
                Status = e.Status, Location = e.Location,
                Description = e.Description, OccurredAt = e.OccurredAt
            }).ToList();
    }
}

public class ListShipmentsByOrderQueryHandler : IRequestHandler<ListShipmentsByOrderQuery, List<ShipmentDto>>
{
    private readonly IShipmentRepository _shipments;
    public ListShipmentsByOrderQueryHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<List<ShipmentDto>> Handle(ListShipmentsByOrderQuery request, CancellationToken cancellationToken)
    {
        var shipments = await _shipments.GetByOrderIdAsync(request.OrderId, cancellationToken);
        return shipments.Select(CreateShipmentCommandHandler.MapToDto).ToList();
    }
}

public class ListCarriersQueryHandler : IRequestHandler<ListCarriersQuery, List<CarrierDto>>
{
    private readonly ICarrierRepository _carriers;
    public ListCarriersQueryHandler(ICarrierRepository carriers) => _carriers = carriers;

    public async Task<List<CarrierDto>> Handle(ListCarriersQuery request, CancellationToken cancellationToken)
    {
        var carriers = await _carriers.GetActiveAsync(cancellationToken);
        return carriers.Select(c => new CarrierDto
        {
            Id = c.Id, Name = c.Name, Code = c.Code,
            IsActive = c.IsActive, BaseRatePerKg = c.BaseRatePerKg
        }).ToList();
    }
}
