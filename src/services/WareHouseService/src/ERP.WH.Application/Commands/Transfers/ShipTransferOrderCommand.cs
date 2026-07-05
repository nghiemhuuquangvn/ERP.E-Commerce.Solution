using MediatR;
using ERP.WH.Application.Interfaces;

namespace ERP.WH.Application.Commands.Transfers;

public class ShipTransferOrderCommand : IRequest<TransferOrderDto>
{
    public Guid TransferOrderId { get; set; }
}

public class ShipTransferOrderCommandHandler : IRequestHandler<ShipTransferOrderCommand, TransferOrderDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public ShipTransferOrderCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<TransferOrderDto> Handle(ShipTransferOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _uow.TransferOrders.GetByIdAsync(request.TransferOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Transfer order {request.TransferOrderId} not found");

        order.Ship();
        await _uow.TransferOrders.UpdateAsync(order, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreateTransferOrderCommandHandler.Map(order);
    }
}

public class CancelTransferOrderCommand : IRequest<TransferOrderDto>
{
    public Guid TransferOrderId { get; set; }
}

public class CancelTransferOrderCommandHandler : IRequestHandler<CancelTransferOrderCommand, TransferOrderDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public CancelTransferOrderCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<TransferOrderDto> Handle(CancelTransferOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _uow.TransferOrders.GetByIdAsync(request.TransferOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Transfer order {request.TransferOrderId} not found");

        order.Cancel();
        await _uow.TransferOrders.UpdateAsync(order, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreateTransferOrderCommandHandler.Map(order);
    }
}
