using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Interfaces;

public interface IInventoryLocationStockRepository
{
    Task<InventoryLocationStock?> GetAsync(Guid productId, Guid locationId, CancellationToken cancellationToken);
    Task<List<InventoryLocationStock>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task AddAsync(InventoryLocationStock stock, CancellationToken cancellationToken);
    Task UpdateAsync(InventoryLocationStock stock, CancellationToken cancellationToken);
}
