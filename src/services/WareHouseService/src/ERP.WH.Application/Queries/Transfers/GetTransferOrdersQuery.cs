using MediatR;
using ERP.WH.Application.Commands.Transfers;

namespace ERP.WH.Application.Queries.Transfers;

public class GetTransferOrderByIdQuery : IRequest<TransferOrderDto?>
{
    public Guid TransferOrderId { get; set; }
}

public class GetTransferOrderByIdQueryHandler : IRequestHandler<GetTransferOrderByIdQuery, TransferOrderDto?>
{
    private readonly ITransferOrderRepository _transferOrderRepository;

    public GetTransferOrderByIdQueryHandler(ITransferOrderRepository transferOrderRepository)
    {
        _transferOrderRepository = transferOrderRepository;
    }

    public async Task<TransferOrderDto?> Handle(GetTransferOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _transferOrderRepository.GetByIdAsync(request.TransferOrderId, cancellationToken);
        return order == null ? null : CreateTransferOrderCommandHandler.Map(order);
    }
}

public class ListTransferOrdersQuery : IRequest<List<TransferOrderDto>>
{
}

public class ListTransferOrdersQueryHandler : IRequestHandler<ListTransferOrdersQuery, List<TransferOrderDto>>
{
    private readonly ITransferOrderRepository _transferOrderRepository;

    public ListTransferOrdersQueryHandler(ITransferOrderRepository transferOrderRepository)
    {
        _transferOrderRepository = transferOrderRepository;
    }

    public async Task<List<TransferOrderDto>> Handle(ListTransferOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _transferOrderRepository.GetAllAsync(cancellationToken);
        return orders.Select(CreateTransferOrderCommandHandler.Map).ToList();
    }
}
