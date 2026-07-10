using MediatR;

namespace ERP.PO.Application.Features.PurchaseOrders.Commands;

public class RejectPurchaseOrderCommand : IRequest<Unit>
{
    public Guid PurchaseOrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RejectedBy { get; set; } = string.Empty;
}
