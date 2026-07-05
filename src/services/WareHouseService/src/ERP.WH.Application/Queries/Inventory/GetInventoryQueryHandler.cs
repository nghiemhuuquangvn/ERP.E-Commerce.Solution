using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.WH.Application.Queries.Inventory;

public class GetInventoryQueryHandler : IRequestHandler<GetInventoryQuery, GetInventoryResponse>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICacheService _cache;

    public GetInventoryQueryHandler(IInventoryRepository inventoryRepository, ICacheService cache)
    {
        _inventoryRepository = inventoryRepository;
        _cache = cache;
    }

    public async Task<GetInventoryResponse> Handle(GetInventoryQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"wh:inventory:{request.ProductId}";

        var cached = await _cache.GetAsync<GetInventoryResponse>(cacheKey, cancellationToken);
        if (cached != null)
            return cached;

        var inventory = await _inventoryRepository.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (inventory == null)
            throw new InvalidOperationException($"Product {request.ProductId} not found");

        var response = new GetInventoryResponse
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

        await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(2), cancellationToken);

        return response;
    }
}
