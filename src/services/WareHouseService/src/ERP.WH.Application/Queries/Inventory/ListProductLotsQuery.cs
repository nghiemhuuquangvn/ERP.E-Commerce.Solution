using ERP.WH.Application.Interfaces;
using MediatR;

namespace ERP.WH.Application.Queries.Inventory;

/// <summary>One lot of an item as Manufacturing needs it: its number (lot numbers of finished goods must be unique per item), where it is and in what state.</summary>
public class ProductLotDto
{
    public Guid LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal RemainingQuantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ManufactureDate { get; set; }
}

/// <summary>All lots of an item, depleted ones included (service-to-service, read only): the check "does this lot number exist?" must also see lots that are used up.</summary>
public class ListProductLotsQuery : IRequest<List<ProductLotDto>>
{
    public Guid ProductId { get; set; }
}

public class ListProductLotsQueryHandler(IWarehouseUnitOfWork uow) : IRequestHandler<ListProductLotsQuery, List<ProductLotDto>>
{
    public async Task<List<ProductLotDto>> Handle(ListProductLotsQuery request, CancellationToken ct)
    {
        var lots = await uow.InventoryLots.GetByProductIdAsync(request.ProductId, includeDepleted: true, ct);
        return lots.OrderBy(l => l.ReceivedDate).Select(l => new ProductLotDto
        {
            LotId = l.LotId, LotNumber = l.LotNumber, WarehouseId = l.WarehouseId, Status = l.Status.ToString(), RemainingQuantity = l.RemainingQuantity,
            ExpiryDate = l.ExpiryDate, ManufactureDate = l.ManufactureDate,
        }).ToList();
    }
}
