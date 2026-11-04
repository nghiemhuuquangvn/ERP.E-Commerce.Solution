using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Queries;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>
/// The purchase order list. The filter, the count and the paging all run in the database query (<see cref="IPurchaseOrderSearch"/>);
/// before 2026-11-04 the whole tenant was loaded and filtered in memory and the answer had no total.
/// </summary>
public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, PurchaseOrderPage>
{
    public const int MaxPageSize = 100;

    private readonly IPurchaseOrderSearch _search;
    private readonly IMapper _mapper;

    public GetPurchaseOrdersQueryHandler(IPurchaseOrderSearch search, IMapper mapper)
    {
        _search = search;
        _mapper = mapper;
    }

    public async Task<PurchaseOrderPage> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var filter = new PurchaseOrderFilter(request.SupplierId, PurchaseOrderFilter.ParseStatus(request.Status), request.DateFrom, request.DateTo, request.Search);
        var pageSize = Math.Min(MaxPageSize, Math.Max(1, request.PageSize));
        var pageNumber = Math.Max(1, request.PageNumber);

        var (items, total) = await _search.SearchAsync(filter, pageNumber, pageSize, cancellationToken);
        return new PurchaseOrderPage
        {
            Items = _mapper.Map<List<PurchaseOrderDto>>(items),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
