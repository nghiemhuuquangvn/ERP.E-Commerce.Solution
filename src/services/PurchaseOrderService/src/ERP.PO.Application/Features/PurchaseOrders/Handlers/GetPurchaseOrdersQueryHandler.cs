using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Queries;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, IEnumerable<PurchaseOrderDto>>
{
    private readonly IMapper _mapper;

    public GetPurchaseOrdersQueryHandler(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<IEnumerable<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new List<PurchaseOrderDto>();
    }
}
