using MediatR;

namespace ERP.PO.Application.Features.PurchaseOrders.Commands;

public class SubmitPurchaseOrderCommand : IRequest<Unit>
{
    public Guid PurchaseOrderId { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
}
