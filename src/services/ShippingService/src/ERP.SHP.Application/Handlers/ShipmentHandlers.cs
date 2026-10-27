using ERP.SHP.Application.Commands;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Queries;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using MediatR;

namespace ERP.SHP.Application.Handlers;

public class CreateShipmentCommandHandler : IRequestHandler<CreateShipmentCommand, ShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    private readonly CarrierBookingService _booking;

    public CreateShipmentCommandHandler(IShippingUnitOfWork uow, CarrierBookingService booking)
    {
        _uow = uow;
        _booking = booking;
    }

    /// <summary>
    /// Creates the shipment AND books it with a carrier (ROADMAP P1-11): the shop's pickup address, the parcel's weight, the cash to collect and the
    /// declared value go to the carrier chosen by a person or, when none was chosen, by the shop's rules and then the cheapest quote. When no carrier
    /// accepts the parcel nothing is saved and <see cref="CarrierBookingException"/> says why (before this the failure was swallowed and a shipment
    /// without tracking number was saved).
    /// </summary>
    public async Task<ShipmentDto> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var sender = await _booking.SenderAsync(cancellationToken);

        string? onlyCarrier = null;
        if (request.CarrierId is { } carrierId)
        {
            var row = await _uow.Carriers.GetByIdAsync(carrierId, cancellationToken)
                ?? throw new InvalidOperationException($"Carrier {carrierId} not found.");
            onlyCarrier = CarrierCodes.Normalize(row.Code)
                ?? throw new CarrierBookingException($"carrier '{row.Code}' has no integration");
        }

        var defaultKg = sender.DefaultItemWeightGrams / 1000m;
        var items = request.Items
            .Select(i => (i.ProductId, i.ProductName, i.Quantity, Weight: i.Weight > 0 ? i.Weight : defaultKg))
            .ToList();

        var r = request.Recipient;
        var recipient = new ShipmentRecipient(
            r.RecipientName, r.RecipientPhone, r.AddressStreet, r.AddressWard,
            r.AddressDistrict, r.AddressProvince, r.AddressPostalCode, r.AddressCountry);

        // The carrier is not known yet: booking sets it (RecordBooking) before anything is saved.
        var shipment = Shipment.Create(request.OrderId, Guid.Empty, "(booking)", recipient, items, request.EstimatedDelivery ?? DateTime.UtcNow.AddDays(3));
        shipment.SetCommercials(request.CodAmount, request.DeclaredValue);

        var parcel = request.Items
            .Select(i => new CarrierParcelItem(i.ProductName, i.Quantity, i.UnitPrice, (int)Math.Round((i.Weight > 0 ? i.Weight : defaultKg) * 1000m)))
            .ToList();
        await _booking.BookAsync(shipment, sender.Address, parcel, new BookingOrder($"SO-{request.OrderId:N}", $"Order {request.OrderId:N}"), onlyCarrier, cancellationToken);

        shipment.FinalizeCreation();
        await _uow.Shipments.AddAsync(shipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
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
    private readonly IShippingUnitOfWork _uow;
    public AssignTrackingNumberCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ShipmentDto> Handle(AssignTrackingNumberCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _uow.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.AssignTrackingNumber(request.TrackingNumber);
        await _uow.Shipments.UpdateAsync(shipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class AddTrackingEventCommandHandler : IRequestHandler<AddTrackingEventCommand, ShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    public AddTrackingEventCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ShipmentDto> Handle(AddTrackingEventCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _uow.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.AddTrackingEvent(request.Status, request.Location, request.Description);
        await _uow.Shipments.UpdateAsync(shipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class ConfirmDeliveryCommandHandler : IRequestHandler<ConfirmDeliveryCommand, ShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    public ConfirmDeliveryCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ShipmentDto> Handle(ConfirmDeliveryCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _uow.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.MarkAsDelivered(request.Location, request.ReceivedByName, request.SignatureUrl, request.PhotoUrl);
        await _uow.Shipments.UpdateAsync(shipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class MarkDeliveryFailedCommandHandler : IRequestHandler<MarkDeliveryFailedCommand, ShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    public MarkDeliveryFailedCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ShipmentDto> Handle(MarkDeliveryFailedCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _uow.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.MarkDeliveryFailed(request.Reason);
        await _uow.Shipments.UpdateAsync(shipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class RetryDeliveryCommandHandler : IRequestHandler<RetryDeliveryCommand, ShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    public RetryDeliveryCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ShipmentDto> Handle(RetryDeliveryCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _uow.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        shipment.RetryDelivery();
        await _uow.Shipments.UpdateAsync(shipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class BulkUpdateTrackingCommandHandler : IRequestHandler<BulkUpdateTrackingCommand, List<ShipmentDto>>
{
    private readonly IShippingUnitOfWork _uow;
    public BulkUpdateTrackingCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<List<ShipmentDto>> Handle(BulkUpdateTrackingCommand request, CancellationToken cancellationToken)
    {
        var results = new List<ShipmentDto>();

        foreach (var update in request.Updates)
        {
            var shipment = await _uow.Shipments.GetByIdAsync(update.ShipmentId, cancellationToken);
            if (shipment == null)
                continue; // best-effort: bulk operation should not fail the whole batch for one bad id

            shipment.AddTrackingEvent(update.Status, update.Location, update.Description);
            await _uow.Shipments.UpdateAsync(shipment, cancellationToken);
            results.Add(CreateShipmentCommandHandler.MapToDto(shipment));
        }

        await _uow.SaveChangesAsync(cancellationToken);
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
