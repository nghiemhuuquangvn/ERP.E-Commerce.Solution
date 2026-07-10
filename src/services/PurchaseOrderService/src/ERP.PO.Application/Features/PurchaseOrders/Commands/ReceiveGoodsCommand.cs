using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.PurchaseOrders.Commands;

public class ReceiveGoodsCommand : IRequest<PurchaseOrderDto>
{
    public Guid PurchaseOrderId { get; set; }

    /// <summary>Key = PurchaseOrderItem.Id, Value = accepted/rejected quantity received on this GRN for that line.</summary>
    public Dictionary<Guid, ReceiveGoodsLineDto> ReceivedQuantities { get; set; } = new();
}

public class ReceiveGoodsLineDto
{
    public decimal QuantityAccepted { get; set; }
    public decimal QuantityRejected { get; set; }
}

public class ScheduleReceiptCommand : IRequest<PurchaseOrderDto>
{
    public Guid PurchaseOrderId { get; set; }
    public DateTime ScheduledDate { get; set; }
}
