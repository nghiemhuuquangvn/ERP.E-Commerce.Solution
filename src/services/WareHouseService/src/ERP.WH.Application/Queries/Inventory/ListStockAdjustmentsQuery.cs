using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Queries.Inventory;

/// <summary>Added alongside the frontend build-out — IStockAdjustmentRepository.ListAsync already existed but no endpoint exposed it, so a Warehouse Manager had no way to see which adjustments are pending approval.</summary>
public class ListStockAdjustmentsQuery : IRequest<List<StockAdjustmentDto>>
{
    public StockAdjustmentStatus? Status { get; set; }
}

public class ListStockAdjustmentsQueryHandler : IRequestHandler<ListStockAdjustmentsQuery, List<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _repository;

    public ListStockAdjustmentsQueryHandler(IStockAdjustmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<StockAdjustmentDto>> Handle(ListStockAdjustmentsQuery request, CancellationToken cancellationToken)
    {
        var adjustments = await _repository.ListAsync(request.Status, cancellationToken);
        return adjustments.Select(RequestStockAdjustmentCommandHandler.Map).ToList();
    }
}
