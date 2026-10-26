using MediatR;

namespace ERP.WH.Application.Queries.Inventory;

public class GetInventoryQuery : IRequest<GetInventoryResponse>
{
    public Guid ProductId { get; set; }
}

public class GetInventoryResponse
{
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int LockedQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public int MinStock { get; set; }
    public int MaxStock { get; set; }
    public int SafetyStock { get; set; }
    public bool IsLowStock { get; set; }
    public required List<LocationStockDto> Locations { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class LocationStockDto
{
    public required string Location { get; set; }
    public int Quantity { get; set; }
    public int Reserved { get; set; }
}
