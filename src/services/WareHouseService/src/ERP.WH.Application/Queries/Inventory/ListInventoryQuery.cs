using MediatR;

namespace ERP.WH.Application.Queries.Inventory;

public class ListInventoryQuery : IRequest<ListInventoryResponse>
{
    public string? Search { get; set; }
    public bool LowStockOnly { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

public class ListInventoryResponse
{
    public int Total { get; set; }
    public required List<InventoryItemDto> Items { get; set; }
}

public class InventoryItemDto
{
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public required string Status { get; set; }
}
