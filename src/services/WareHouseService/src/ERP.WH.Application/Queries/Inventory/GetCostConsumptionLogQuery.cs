using MediatR;
using ERP.WH.Application.Services;

namespace ERP.WH.Application.Queries.Inventory;

public class GetCostConsumptionLogQuery : IRequest<List<CostConsumptionLogDto>>
{
    public Guid ProductId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CostConsumptionLogDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LotId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public Guid SourceOrderId { get; set; }
    public DateTime ConsumedAt { get; set; }
}

public class GetCostConsumptionLogQueryHandler : IRequestHandler<GetCostConsumptionLogQuery, List<CostConsumptionLogDto>>
{
    private readonly ICostConsumptionLogRepository _repository;

    public GetCostConsumptionLogQueryHandler(ICostConsumptionLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<CostConsumptionLogDto>> Handle(GetCostConsumptionLogQuery request, CancellationToken cancellationToken)
    {
        var logs = await _repository.GetByProductIdAsync(request.ProductId, request.StartDate, request.EndDate, cancellationToken);

        return logs.Select(l => new CostConsumptionLogDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            LotId = l.LotId,
            Quantity = l.Quantity,
            UnitCost = l.UnitCost,
            TotalCost = l.TotalCost,
            SourceOrderId = l.SourceOrderId,
            ConsumedAt = l.ConsumedAt
        }).ToList();
    }
}
