using ERP.SHP.Application.Commands;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Queries;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
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

    public static ShipmentDto MapToDto(Shipment s) => new()
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
        CodAmount = s.CodAmount,
        DeclaredValue = s.DeclaredValue,
        CarrierFee = s.CarrierFee,
        CarrierOrderCode = s.CarrierOrderCode,
        SelectionNote = s.SelectionNote,
        BookingError = s.BookingError,
        ReviewNote = s.ReviewNote,
        LastCarrierSyncAt = s.LastCarrierSyncAt,
        ParcelNo = s.ParcelNo,
        ParcelCount = s.ParcelCount,
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

public class ListShipmentsQueryHandler : IRequestHandler<ListShipmentsQuery, ShipmentListResult>
{
    private readonly IShipmentRepository _shipments;
    public ListShipmentsQueryHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<ShipmentListResult> Handle(ListShipmentsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        ShipmentStatus? status = Enum.TryParse<ShipmentStatus>(request.Status, ignoreCase: true, out var parsed) ? parsed : null;

        var (items, total) = await _shipments.ListAsync((page - 1) * size, size, status, request.Search, request.NeedsReview, cancellationToken);
        return new ShipmentListResult
        {
            Items = items.Select(CreateShipmentCommandHandler.MapToDto).ToList(),
            TotalCount = total,
            PageNumber = page,
            PageSize = size
        };
    }
}

public class CancelShipmentAtCarrierCommand : IRequest<ShipmentDto>
{
    public Guid ShipmentId { get; set; }
    public string Reason { get; set; } = "cancelled by the shop";
}

/// <summary>A person cancels a parcel the carrier has not collected. The carrier's refusal comes back as a business error (HTTP 400) with its reason.</summary>
public class CancelShipmentAtCarrierCommandHandler : IRequestHandler<CancelShipmentAtCarrierCommand, ShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    private readonly CarrierTrackingService _tracking;

    public CancelShipmentAtCarrierCommandHandler(IShippingUnitOfWork uow, CarrierTrackingService tracking)
    {
        _uow = uow;
        _tracking = tracking;
    }

    public async Task<ShipmentDto> Handle(CancelShipmentAtCarrierCommand request, CancellationToken cancellationToken)
    {
        var shipment = (await _uow.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken))
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");
        var (cancelled, problem) = await _tracking.CancelAtCarrierAsync(shipment, request.Reason, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);   // keeps the review note when the carrier refused
        if (!cancelled)
            throw new CarrierBookingException(problem ?? "the shipment could not be cancelled");
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}

public class RebookShipmentCommand : IRequest<ShipmentDto>
{
    public Guid ShipmentId { get; set; }
    /// <summary>A carrier chosen by a person (its Carrier row id); null = the rules, then the cheapest quote.</summary>
    public Guid? CarrierId { get; set; }
}

/// <summary>Books again a shipment that never got a carrier's tracking number (created before P1-11, or its booking was fixed afterwards).</summary>
public class RebookShipmentCommandHandler : IRequestHandler<RebookShipmentCommand, ShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    private readonly CarrierBookingService _booking;

    public RebookShipmentCommandHandler(IShippingUnitOfWork uow, CarrierBookingService booking)
    {
        _uow = uow;
        _booking = booking;
    }

    public async Task<ShipmentDto> Handle(RebookShipmentCommand request, CancellationToken cancellationToken)
    {
        var shipment = (await _uow.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken))
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");
        if (!string.IsNullOrEmpty(shipment.TrackingNumber))
            throw new CarrierBookingException("this shipment already has a carrier's tracking number");
        if (shipment.Status is not (ShipmentStatus.Pending or ShipmentStatus.ReadyForPickup))
            throw new CarrierBookingException($"a shipment that is {shipment.Status} cannot be booked");

        var sender = await _booking.SenderAsync(cancellationToken);
        string? only = null;
        if (request.CarrierId is { } id)
        {
            var row = await _uow.Carriers.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException($"Carrier {id} not found.");
            only = CarrierCodes.Normalize(row.Code) ?? throw new CarrierBookingException($"carrier '{row.Code}' has no integration");
        }

        var parcel = shipment.Items.Select(i => new CarrierParcelItem(i.ProductName, i.Quantity, 0m, (int)Math.Round((i.Weight > 0 ? i.Weight : sender.DefaultItemWeightGrams / 1000m) * 1000m))).ToList();
        await _booking.BookAsync(shipment, sender.Address, parcel, new BookingOrder(shipment.BookingReference, $"Order {shipment.OrderId:N}"), only, cancellationToken);

        await _uow.Shipments.UpdateAsync(shipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreateShipmentCommandHandler.MapToDto(shipment);
    }
}
