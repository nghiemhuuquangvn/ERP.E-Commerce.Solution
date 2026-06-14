using MediatR;

namespace ERP.PO.Application.Features.PurchaseOrders.Commands;

public class ApprovePurchaseOrderCommand : IRequest<Unit>
{
    public Guid PurchaseOrderId { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
}
