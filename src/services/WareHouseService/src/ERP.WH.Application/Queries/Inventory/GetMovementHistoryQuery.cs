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
    public List<StockMovementDto> Items { get; set; }
}

public class StockMovementDto
{
    public Guid MovementId { get; set; }
    public Guid ProductId { get; set; }
    public string MovementType { get; set; }
    public int Quantity { get; set; }
    public string FromLocation { get; set; }
    public string ToLocation { get; set; }
    public string Reference { get; set; }
    public string Reason { get; set; }
    public DateTime RecordedAt { get; set; }
}
