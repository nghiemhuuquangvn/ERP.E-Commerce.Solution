using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

public class RecordStockMovementCommand : IRequest<RecordStockMovementResponse>
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string MovementType { get; set; }
    public string FromLocation { get; set; }
    public string ToLocation { get; set; }
    public string Reference { get; set; }
    public string Reason { get; set; }
    public Guid? SourceOrderId { get; set; }
}

public class RecordStockMovementResponse
{
    public Guid MovementId { get; set; }
    public string MovementType { get; set; }
    public int Quantity { get; set; }
    public DateTime RecordedAt { get; set; }
}
