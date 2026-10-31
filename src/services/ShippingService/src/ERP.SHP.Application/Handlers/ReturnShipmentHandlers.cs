using ERP.SHP.Application.Commands;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using MediatR;

namespace ERP.SHP.Application.Handlers;

public class InitiateReturnCommandHandler : IRequestHandler<InitiateReturnCommand, ReturnShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;

    public InitiateReturnCommandHandler(IShippingUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ReturnShipmentDto> Handle(InitiateReturnCommand request, CancellationToken cancellationToken)
    {
        var original = await _uow.Shipments.GetByIdAsync(request.OriginalShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.OriginalShipmentId} not found.");

        var returnShipment = ReturnShipment.Create(original.Id, original.OrderId, request.Reason);
        await _uow.ReturnShipments.AddAsync(returnShipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return MapToDto(returnShipment);
    }

    public static ReturnShipmentDto MapToDto(ReturnShipment r) => new()
    {
        Id = r.Id,
        OriginalShipmentId = r.OriginalShipmentId,
        OrderId = r.OrderId,
        Reason = r.Reason,
        TrackingNumber = r.TrackingNumber,
        Status = r.Status.ToString(),
        PickedUpAt = r.PickedUpAt,
        ReceivedAt = r.ReceivedAt,
        CarrierName = r.CarrierName,
        CarrierOrderCode = r.CarrierOrderCode,
        CarrierFee = r.CarrierFee,
        BookingError = r.BookingError,
        CarrierId = r.CarrierId,
        LastCarrierSyncAt = r.LastCarrierSyncAt,
        CreatedAt = r.CreatedAt
    };
}

public class AssignReturnTrackingNumberCommandHandler : IRequestHandler<AssignReturnTrackingNumberCommand, ReturnShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    public AssignReturnTrackingNumberCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ReturnShipmentDto> Handle(AssignReturnTrackingNumberCommand request, CancellationToken cancellationToken)
    {
        var returnShipment = await _uow.ReturnShipments.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Return shipment {request.ReturnShipmentId} not found.");

        returnShipment.AssignTrackingNumber(request.TrackingNumber);
        await _uow.ReturnShipments.UpdateAsync(returnShipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

public class MarkReturnPickedUpCommandHandler : IRequestHandler<MarkReturnPickedUpCommand, ReturnShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    public MarkReturnPickedUpCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ReturnShipmentDto> Handle(MarkReturnPickedUpCommand request, CancellationToken cancellationToken)
    {
        var returnShipment = await _uow.ReturnShipments.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Return shipment {request.ReturnShipmentId} not found.");

        returnShipment.MarkPickedUp();
        await _uow.ReturnShipments.UpdateAsync(returnShipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

public class MarkReturnReceivedCommandHandler : IRequestHandler<MarkReturnReceivedCommand, ReturnShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    public MarkReturnReceivedCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ReturnShipmentDto> Handle(MarkReturnReceivedCommand request, CancellationToken cancellationToken)
    {
        var returnShipment = await _uow.ReturnShipments.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Return shipment {request.ReturnShipmentId} not found.");

        await ParcelProgress.TellOthersPendingAsync(_uow, returnShipment, cancellationToken);
        returnShipment.MarkReceived();
        await _uow.ReturnShipments.UpdateAsync(returnShipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

public class CancelReturnCommandHandler : IRequestHandler<CancelReturnCommand, ReturnShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    public CancelReturnCommandHandler(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ReturnShipmentDto> Handle(CancelReturnCommand request, CancellationToken cancellationToken)
    {
        var returnShipment = await _uow.ReturnShipments.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Return shipment {request.ReturnShipmentId} not found.");

        returnShipment.Cancel();
        await _uow.ReturnShipments.UpdateAsync(returnShipment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

/// <summary>Books the order's missing reverse parcels again through the one booking path (ReturnBookingService); the answer is this return as it is afterwards (a refusal stays on it as BookingError).</summary>
public class RebookReturnCommandHandler : IRequestHandler<RebookReturnCommand, ReturnShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    private readonly ReturnBookingService _booking;
    public RebookReturnCommandHandler(IShippingUnitOfWork uow, ReturnBookingService booking) { _uow = uow; _booking = booking; }

    public async Task<ReturnShipmentDto> Handle(RebookReturnCommand request, CancellationToken cancellationToken)
    {
        var returned = await _uow.ReturnShipments.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Return shipment {request.ReturnShipmentId} not found.");
        if (returned.Status != ERP.SHP.Domain.Entities.ReturnShipmentStatus.Requested)
            throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Shipping.Returns", $"A return that is {returned.Status} cannot be booked again.");
        if (!string.IsNullOrEmpty(returned.TrackingNumber))
            return InitiateReturnCommandHandler.MapToDto(returned);   // already booked: nothing to do

        var booked = await _booking.BookReturnsAsync(returned.OrderId, returned.Reason, cancellationToken);
        var now = booked.FirstOrDefault(r => r.Id == returned.Id) ?? returned;
        return InitiateReturnCommandHandler.MapToDto(now);
    }
}

public class CancelReturnAtCarrierCommandHandler : IRequestHandler<CancelReturnAtCarrierCommand, ReturnShipmentDto>
{
    private readonly IShippingUnitOfWork _uow;
    private readonly CarrierTrackingService _tracking;
    public CancelReturnAtCarrierCommandHandler(IShippingUnitOfWork uow, CarrierTrackingService tracking) { _uow = uow; _tracking = tracking; }

    public async Task<ReturnShipmentDto> Handle(CancelReturnAtCarrierCommand request, CancellationToken cancellationToken)
    {
        var returned = await _uow.ReturnShipments.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Return shipment {request.ReturnShipmentId} not found.");
        if (returned.Status == ERP.SHP.Domain.Entities.ReturnShipmentStatus.Cancelled)
            return InitiateReturnCommandHandler.MapToDto(returned);
        if (returned.Status != ERP.SHP.Domain.Entities.ReturnShipmentStatus.Requested)
            throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Shipping.Returns", $"The goods are already {returned.Status}: the carrier no longer cancels this pickup.");

        if (!string.IsNullOrEmpty(returned.TrackingNumber) && returned.CarrierId.HasValue)
        {
            var (cancelled, problem) = await _tracking.CancelBookingAsync(returned.CarrierId.Value, returned.TrackingNumber, $"Return cancelled: {returned.Reason}", cancellationToken);
            if (!cancelled)
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Shipping.Returns", $"The carrier did not cancel the pickup: {problem}");
        }

        returned.Cancel();
        await _uow.ReturnShipments.UpdateAsync(returned, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returned);
    }
}
