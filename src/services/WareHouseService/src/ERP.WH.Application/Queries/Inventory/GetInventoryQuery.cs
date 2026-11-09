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
    public decimal TotalQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal LockedQuantity { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal MinStock { get; set; }
    public decimal MaxStock { get; set; }
    public decimal SafetyStock { get; set; }
    public bool IsLowStock { get; set; }
    public required List<LocationStockDto> Locations { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class LocationStockDto
{
    public required string Location { get; set; }
    public decimal Quantity { get; set; }
    public decimal Reserved { get; set; }
}
