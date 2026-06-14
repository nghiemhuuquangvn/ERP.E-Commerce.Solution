using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.PurchaseOrders.Commands;

public class AddItemCommand : IRequest<PurchaseOrderDto>
{
    public Guid PurchaseOrderId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string AddedBy { get; set; } = string.Empty;
}
