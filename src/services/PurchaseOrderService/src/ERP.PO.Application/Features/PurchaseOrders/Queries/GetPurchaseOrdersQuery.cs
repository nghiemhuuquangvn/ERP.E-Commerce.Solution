using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.PurchaseOrders.Queries;

public class GetPurchaseOrdersQuery : IRequest<IEnumerable<PurchaseOrderDto>>
{
    public Guid? SupplierId { get; set; }
    public string? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
