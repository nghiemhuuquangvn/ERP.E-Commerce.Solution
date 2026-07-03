using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Queries.Inventory;

public class InventoryValuationLotDto
{
    public string LotNumber { get; set; } = string.Empty;
    public int RemainingQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineValue { get; set; }
    public DateTime ReceivedDate { get; set; }
}

public class GetInventoryValuationResponse
{
    public Guid ProductId { get; set; }
    public string ValuationMethod { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageUnitCost { get; set; }
    public List<InventoryValuationLotDto> Lots { get; set; } = new();
}

public class GetInventoryValuationQuery : IRequest<GetInventoryValuationResponse>
{
    public Guid ProductId { get; set; }
}

/// <summary>
/// Derives valuation from the same cost layers RecordStockMovementCommandHandler
/// depletes on sale (FIFO oldest-lot-first, LIFO newest-lot-first) so the reported
/// value matches which lots are actually still on hand. Average-cost products carry
/// no discrete layers — Inventory.Cost is already a running weighted average
/// recalculated on every receipt (see Inventory.ReceiveStock).
/// </summary>
public class GetInventoryValuationQueryHandler : IRequestHandler<GetInventoryValuationQuery, GetInventoryValuationResponse>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryLotRepository _lotRepository;

    public GetInventoryValuationQueryHandler(IInventoryRepository inventoryRepository, IInventoryLotRepository lotRepository)
    {
        _inventoryRepository = inventoryRepository;
        _lotRepository = lotRepository;
    }

    public async Task<GetInventoryValuationResponse> Handle(GetInventoryValuationQuery request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByProductIdAsync(request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} not found");

        if (inventory.ValuationMethod == ValuationMethod.Average)
        {
            return new GetInventoryValuationResponse
            {
                ProductId = inventory.ProductId,
                ValuationMethod = nameof(ValuationMethod.Average),
                TotalQuantity = inventory.TotalQuantity,
                TotalValue = inventory.TotalQuantity * inventory.Cost,
                AverageUnitCost = inventory.Cost,
                Lots = new List<InventoryValuationLotDto>()
            };
        }

        var lots = await _lotRepository.GetByProductIdAsync(request.ProductId, includeDepleted: false, cancellationToken);
        var ordered = inventory.ValuationMethod == ValuationMethod.LIFO
            ? lots.OrderByDescending(l => l.ReceivedDate).ToList()
            : lots.OrderBy(l => l.ReceivedDate).ToList();

        var lotDtos = ordered.Select(l => new InventoryValuationLotDto
        {
            LotNumber = l.LotNumber,
            RemainingQuantity = l.RemainingQuantity,
            UnitCost = l.UnitCost,
            LineValue = l.RemainingQuantity * l.UnitCost,
            ReceivedDate = l.ReceivedDate
        }).ToList();

        var totalValue = lotDtos.Sum(l => l.LineValue);
        var totalLotQuantity = lotDtos.Sum(l => l.RemainingQuantity);

        return new GetInventoryValuationResponse
        {
            ProductId = inventory.ProductId,
            ValuationMethod = inventory.ValuationMethod.ToString(),
            TotalQuantity = inventory.TotalQuantity,
            TotalValue = totalValue,
            AverageUnitCost = totalLotQuantity > 0 ? totalValue / totalLotQuantity : inventory.Cost,
            Lots = lotDtos
        };
    }
}
