using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Queries;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto>
{
    private readonly IMapper _mapper;

    public GetPurchaseOrderByIdQueryHandler(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<PurchaseOrderDto> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return _mapper.Map<PurchaseOrderDto>(new object());
    }
}
