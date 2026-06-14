using MediatR;

namespace ERP.WH.Application.Queries.Inventory;

public class GetMovementHistoryQueryHandler : IRequestHandler<GetMovementHistoryQuery, GetMovementHistoryResponse>
{
    private readonly IStockMovementRepository _movementRepository;

    public GetMovementHistoryQueryHandler(IStockMovementRepository movementRepository)
    {
        _movementRepository = movementRepository;
    }

    public async Task<GetMovementHistoryResponse> Handle(GetMovementHistoryQuery request, CancellationToken cancellationToken)
    {
        var movements = await _movementRepository.GetByProductIdAsync(
            request.ProductId,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        var total = movements.Count;
        var items = movements
            .OrderByDescending(m => m.RecordedAt)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(m => new StockMovementDto
            {
                MovementId = m.MovementId,
                ProductId = m.ProductId,
                MovementType = m.MovementType.ToString(),
                Quantity = m.Quantity,
                FromLocation = m.FromLocation,
                ToLocation = m.ToLocation,
                Reference = m.Reference,
                Reason = m.Reason,
                RecordedAt = m.RecordedAt
            })
            .ToList();

        return new GetMovementHistoryResponse
        {
            Total = total,
            Items = items
        };
    }
}
