using ERP.SHP.Application.Commands;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using MediatR;

namespace ERP.SHP.Application.Handlers;

public class InitiateReturnCommandHandler : IRequestHandler<InitiateReturnCommand, ReturnShipmentDto>
{
    private readonly IReturnShipmentRepository _returns;
    private readonly IShipmentRepository _shipments;

    public InitiateReturnCommandHandler(IReturnShipmentRepository returns, IShipmentRepository shipments)
    {
        _returns = returns;
        _shipments = shipments;
    }

    public async Task<ReturnShipmentDto> Handle(InitiateReturnCommand request, CancellationToken cancellationToken)
    {
        var original = await _shipments.GetByIdAsync(request.OriginalShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.OriginalShipmentId} not found.");

        var returnShipment = ReturnShipment.Create(original.Id, original.OrderId, request.Reason);
        await _returns.AddAsync(returnShipment, cancellationToken);

        return MapToDto(returnShipment);
    }

    internal static ReturnShipmentDto MapToDto(ReturnShipment r) => new()
    {
        Id = r.Id,
        OriginalShipmentId = r.OriginalShipmentId,
        OrderId = r.OrderId,
        Reason = r.Reason,
        TrackingNumber = r.TrackingNumber,
        Status = r.Status.ToString(),
        PickedUpAt = r.PickedUpAt,
        ReceivedAt = r.ReceivedAt,
        CreatedAt = r.CreatedAt
    };
}

public class AssignReturnTrackingNumberCommandHandler : IRequestHandler<AssignReturnTrackingNumberCommand, ReturnShipmentDto>
{
    private readonly IReturnShipmentRepository _returns;
    public AssignReturnTrackingNumberCommandHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<ReturnShipmentDto> Handle(AssignReturnTrackingNumberCommand request, CancellationToken cancellationToken)
    {
        var returnShipment = await _returns.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Return shipment {request.ReturnShipmentId} not found.");

        returnShipment.AssignTrackingNumber(request.TrackingNumber);
        await _returns.UpdateAsync(returnShipment, cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

public class MarkReturnPickedUpCommandHandler : IRequestHandler<MarkReturnPickedUpCommand, ReturnShipmentDto>
{
    private readonly IReturnShipmentRepository _returns;
    public MarkReturnPickedUpCommandHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<ReturnShipmentDto> Handle(MarkReturnPickedUpCommand request, CancellationToken cancellationToken)
    {
        var returnShipment = await _returns.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Return shipment {request.ReturnShipmentId} not found.");

        returnShipment.MarkPickedUp();
        await _returns.UpdateAsync(returnShipment, cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

public class MarkReturnReceivedCommandHandler : IRequestHandler<MarkReturnReceivedCommand, ReturnShipmentDto>
{
    private readonly IReturnShipmentRepository _returns;
    public MarkReturnReceivedCommandHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<ReturnShipmentDto> Handle(MarkReturnReceivedCommand request, CancellationToken cancellationToken)
    {
        var returnShipment = await _returns.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Return shipment {request.ReturnShipmentId} not found.");

        returnShipment.MarkReceived();
        await _returns.UpdateAsync(returnShipment, cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

public class CancelReturnCommandHandler : IRequestHandler<CancelReturnCommand, ReturnShipmentDto>
{
    private readonly IReturnShipmentRepository _returns;
    public CancelReturnCommandHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<ReturnShipmentDto> Handle(CancelReturnCommand request, CancellationToken cancellationToken)
    {
        var returnShipment = await _returns.GetByIdAsync(request.ReturnShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Return shipment {request.ReturnShipmentId} not found.");

        returnShipment.Cancel();
        await _returns.UpdateAsync(returnShipment, cancellationToken);
        return InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}
