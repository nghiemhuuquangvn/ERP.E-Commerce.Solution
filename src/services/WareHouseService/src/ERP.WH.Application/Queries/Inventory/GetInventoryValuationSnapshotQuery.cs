using MediatR;
using ERP.WH.Application.Commands.Inventory;

namespace ERP.WH.Application.Queries.Inventory;

public class GetInventoryValuationSnapshotQuery : IRequest<InventoryValuationSnapshotDto>
{
    public required string PeriodCode { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class GetInventoryValuationSnapshotQueryHandler : IRequestHandler<GetInventoryValuationSnapshotQuery, InventoryValuationSnapshotDto>
{
    private readonly IInventoryValuationSnapshotRepository _snapshots;

    public GetInventoryValuationSnapshotQueryHandler(IInventoryValuationSnapshotRepository snapshots)
    {
        _snapshots = snapshots;
    }

    public async Task<InventoryValuationSnapshotDto> Handle(GetInventoryValuationSnapshotQuery request, CancellationToken cancellationToken)
    {
        var snapshot = await _snapshots.GetByPeriodAsync(request.PeriodCode, request.WarehouseId, cancellationToken)
            ?? throw new InvalidOperationException($"No closed valuation snapshot found for period {request.PeriodCode}");

        return CloseInventoryValuationSnapshotCommandHandler.Map(snapshot);
    }
}
