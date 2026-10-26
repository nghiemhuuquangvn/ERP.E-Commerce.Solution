using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Commands.Locations;
using ERP.WH.Application.Interfaces;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.WH.Application.Queries.Inventory;

public class GetInventoryQueryHandler : IRequestHandler<GetInventoryQuery, GetInventoryResponse>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryLocationStockRepository _locationStockRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly ICacheService _cache;

    public GetInventoryQueryHandler(
        IInventoryRepository inventoryRepository,
        IInventoryLocationStockRepository locationStockRepository,
        ILocationRepository locationRepository,
        ICacheService cache)
    {
        _inventoryRepository = inventoryRepository;
        _locationStockRepository = locationStockRepository;
        _locationRepository = locationRepository;
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

        // Real bin-level breakdown where it exists (InventoryLocationStock, added
        // 2026-07-27) — previously this always returned one fake "DEFAULT_LOCATION" row
        // summing the whole product-level rollup, regardless of actual location data.
        var locationStocks = await _locationStockRepository.GetByProductIdAsync(request.ProductId, cancellationToken);
        List<LocationStockDto> locations;
        if (locationStocks.Count > 0)
        {
            locations = new List<LocationStockDto>();
            foreach (var stock in locationStocks)
            {
                var location = await _locationRepository.GetByIdAsync(stock.LocationId, cancellationToken);
                locations.Add(new LocationStockDto
                {
                    Location = location?.LocationCode ?? stock.LocationId.ToString(),
                    Quantity = stock.Quantity,
                    Reserved = stock.ReservedQuantity
                });
            }
        }
        else
        {
            // No bin-level data tracked yet for this product — fall back to the
            // product-level rollup as a single row, same as the previous behavior.
            locations = new List<LocationStockDto>
            {
                new LocationStockDto
                {
                    Location = "DEFAULT_LOCATION",
                    Quantity = inventory.TotalQuantity,
                    Reserved = inventory.ReservedQuantity
                }
            };
        }

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
            MinStock = inventory.MinStock,
            MaxStock = inventory.MaxStock,
            SafetyStock = inventory.SafetyStock,
            IsLowStock = inventory.IsLowStock(),
            Locations = locations,
            LastUpdated = inventory.UpdatedAt
        };

        await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(2), cancellationToken);

        return response;
    }
}
