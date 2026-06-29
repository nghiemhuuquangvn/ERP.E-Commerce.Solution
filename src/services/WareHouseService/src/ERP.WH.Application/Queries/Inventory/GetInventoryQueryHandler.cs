using MediatR;
using ERP.WH.Application.Commands.Inventory;

namespace ERP.WH.Application.Queries.Inventory;

public class GetInventoryQueryHandler : IRequestHandler<GetInventoryQuery, GetInventoryResponse>
{
    private readonly IInventoryRepository _inventoryRepository;

    public GetInventoryQueryHandler(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public async Task<GetInventoryResponse> Handle(GetInventoryQuery request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (inventory == null)
            throw new InvalidOperationException($"Product {request.ProductId} not found");

        return new GetInventoryResponse
        {
            ProductId = inventory.ProductId,
            ProductName = inventory.ProductName,
            TotalQuantity = inventory.TotalQuantity,
            AvailableQuantity = inventory.AvailableQuantity,
            ReservedQuantity = inventory.ReservedQuantity,
            LockedQuantity = inventory.LockedQuantity,
            ReorderPoint = inventory.ReorderPoint,
            ReorderQuantity = inventory.ReorderQuantity,
            Locations = new List<LocationStockDto>
            {
                new LocationStockDto
                {
                    Location = "DEFAULT_LOCATION",
                    Quantity = inventory.TotalQuantity,
                    Reserved = inventory.ReservedQuantity
                }
            },
            LastUpdated = inventory.UpdatedAt
        };
    }
}
