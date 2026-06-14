using MediatR;

namespace ERP.PO.Application.Features.PurchaseOrders.Commands;

public class CancelPurchaseOrderCommand : IRequest<Unit>
{
    public Guid PurchaseOrderId { get; set; }
    public string CancelledBy { get; set; } = string.Empty;
}
