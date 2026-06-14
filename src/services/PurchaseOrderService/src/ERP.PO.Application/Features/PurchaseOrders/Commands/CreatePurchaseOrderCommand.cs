using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to create a new purchase order.
/// CreatedBy is extracted from IUserContextService (API Gateway headers), not from request.
/// </summary>
public class CreatePurchaseOrderCommand : IRequest<PurchaseOrderDto>
{
    public string PONumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}
