using MediatR;
using ERP.WH.Application.Commands.Inventory;

namespace ERP.WH.Application.Queries.Inventory;

public class ListInventoryQueryHandler : IRequestHandler<ListInventoryQuery, ListInventoryResponse>
{
    private readonly IInventoryRepository _inventoryRepository;

    public ListInventoryQueryHandler(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public async Task<ListInventoryResponse> Handle(ListInventoryQuery request, CancellationToken cancellationToken)
    {
        var allInventories = await _inventoryRepository.GetAllAsync(cancellationToken);

        var filtered = allInventories
            .Where(i => string.IsNullOrEmpty(request.Search) ||
                       i.ProductName.Contains(request.Search, StringComparison.OrdinalIgnoreCase))
            .Where(i => !request.LowStockOnly || i.IsLowStock())
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();

        var items = filtered.Select(i => new InventoryItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            TotalQuantity = i.TotalQuantity,
            AvailableQuantity = i.AvailableQuantity,
            ReorderPoint = i.ReorderPoint,
            MinStock = i.MinStock,
            MaxStock = i.MaxStock,
            SafetyStock = i.SafetyStock,
            ProductSku = i.ProductSku,
            Status = i.IsLowStock() ? "Low Stock" : "OK"
        }).ToList();

        return new ListInventoryResponse
        {
            Total = allInventories.Count,
            Items = items
        };
    }
}
