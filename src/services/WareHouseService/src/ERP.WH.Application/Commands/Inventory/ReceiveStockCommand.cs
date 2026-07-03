using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

public class ReceiveStockCommand : IRequest<ReceiveStockResponse>
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public Guid? LocationId { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public List<string>? SerialNumbers { get; set; }
    public string? Reference { get; set; }
    public Guid? PurchaseOrderId { get; set; }
}

public class ReceiveStockResponse
{
    public Guid ProductId { get; set; }
    public int TotalQuantity { get; set; }
    public Guid MovementId { get; set; }
    public Guid? LotId { get; set; }
    public DateTime ReceivedAt { get; set; }
}
