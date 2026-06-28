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

    public CreateShipmentCommandHandler(IShipmentRepository shipments, ICarrierRepository carriers)
    {
        _shipments = shipments;
        _carriers = carriers;
    }

    public async Task<ShipmentDto> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var carrier = await _carriers.GetByIdAsync(request.CarrierId, cancellationToken)
            ?? throw new InvalidOperationException($"Carrier {request.CarrierId} not found.");

        var items = request.Items
            .Select(i => (i.ProductId, i.ProductName, i.Quantity, i.Weight))
            .ToList();

        var shipment = Shipment.Create(
            request.OrderId, request.CarrierId, carrier.Name,
            request.ShippingAddress, items, request.EstimatedDelivery);

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
        ShippingAddress = s.ShippingAddress,
        EstimatedDelivery = s.EstimatedDelivery,
        PickedUpAt = s.PickedUpAt,
        DeliveredAt = s.DeliveredAt,
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

        shipment.MarkAsDelivered(request.Location);
        await _shipments.UpdateAsync(shipment, cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
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
