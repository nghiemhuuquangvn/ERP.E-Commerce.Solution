using MediatR;

namespace ERP.WH.Application.Queries.Inventory;

public class GetMovementHistoryQuery : IRequest<GetMovementHistoryResponse>
{
    public Guid ProductId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

public class GetMovementHistoryResponse
{
    public int Total { get; set; }
    public required List<StockMovementDto> Items { get; set; }
}

public class StockMovementDto
{
    public Guid MovementId { get; set; }
    public Guid ProductId { get; set; }
    public required string MovementType { get; set; }
    public decimal Quantity { get; set; }
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public DateTime RecordedAt { get; set; }

    // From W3; null for older rows ("not recorded")
    public Guid? WarehouseId { get; set; }
    public Guid? LotId { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? ValueDelta { get; set; }
    public decimal? StockDelta { get; set; }

    /// <summary>The lots the movement acted on (W7); empty for lot-less stock and for movements booked before W7.</summary>
    public List<ERP.WH.Application.Commands.Inventory.MovementLotDto> Lots { get; set; } = [];
}
