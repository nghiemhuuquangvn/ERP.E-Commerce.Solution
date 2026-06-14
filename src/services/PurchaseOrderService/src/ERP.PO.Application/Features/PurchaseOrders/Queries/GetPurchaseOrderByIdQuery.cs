using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.PurchaseOrders.Queries;

public class GetPurchaseOrderByIdQuery : IRequest<PurchaseOrderDto>
{
    public Guid PurchaseOrderId { get; set; }
}
