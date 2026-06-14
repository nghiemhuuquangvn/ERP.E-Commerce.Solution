using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.PurchaseOrders.Commands;

public class RemoveItemCommand : IRequest<PurchaseOrderDto>
{
    public Guid PurchaseOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string RemovedBy { get; set; } = string.Empty;
}
