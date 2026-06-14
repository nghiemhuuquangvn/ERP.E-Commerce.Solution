using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class RemoveItemCommandHandler : IRequestHandler<RemoveItemCommand, PurchaseOrderDto>
{
    private readonly IMapper _mapper;

    public RemoveItemCommandHandler(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<PurchaseOrderDto> Handle(RemoveItemCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return _mapper.Map<PurchaseOrderDto>(new object());
    }
}
