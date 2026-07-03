using MediatR;

namespace ERP.WH.Application.Commands.Transfers;

public class ShipTransferOrderCommand : IRequest<TransferOrderDto>
{
    public Guid TransferOrderId { get; set; }
}

public class ShipTransferOrderCommandHandler : IRequestHandler<ShipTransferOrderCommand, TransferOrderDto>
{
    private readonly ITransferOrderRepository _transferOrderRepository;

    public ShipTransferOrderCommandHandler(ITransferOrderRepository transferOrderRepository)
    {
        _transferOrderRepository = transferOrderRepository;
    }

    public async Task<TransferOrderDto> Handle(ShipTransferOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _transferOrderRepository.GetByIdAsync(request.TransferOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Transfer order {request.TransferOrderId} not found");

        order.Ship();
        await _transferOrderRepository.UpdateAsync(order, cancellationToken);

        return CreateTransferOrderCommandHandler.Map(order);
    }
}

public class CancelTransferOrderCommand : IRequest<TransferOrderDto>
{
    public Guid TransferOrderId { get; set; }
}

public class CancelTransferOrderCommandHandler : IRequestHandler<CancelTransferOrderCommand, TransferOrderDto>
{
    private readonly ITransferOrderRepository _transferOrderRepository;

    public CancelTransferOrderCommandHandler(ITransferOrderRepository transferOrderRepository)
    {
        _transferOrderRepository = transferOrderRepository;
    }

    public async Task<TransferOrderDto> Handle(CancelTransferOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _transferOrderRepository.GetByIdAsync(request.TransferOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Transfer order {request.TransferOrderId} not found");

        order.Cancel();
        await _transferOrderRepository.UpdateAsync(order, cancellationToken);

        return CreateTransferOrderCommandHandler.Map(order);
    }
}
