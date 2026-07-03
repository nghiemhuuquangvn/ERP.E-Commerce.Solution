using MediatR;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Commands.Inventory;

public class AdjustStockCommand : IRequest<AdjustStockResponse>
{
    public Guid ProductId { get; set; }
    public int QuantityDelta { get; set; }
    public AdjustmentReason Reason { get; set; }
    public string? Notes { get; set; }
}

public class AdjustStockResponse
{
    public Guid ProductId { get; set; }
    public int PreviousQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public Guid MovementId { get; set; }
    public DateTime AdjustedAt { get; set; }
}
