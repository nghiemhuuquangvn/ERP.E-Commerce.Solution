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
