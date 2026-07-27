using ERP.WH.Domain.Aggregates.Location;

namespace ERP.WH.Application.Interfaces;

public interface IWarehouseRepository
{
    Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Warehouse>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken);
    Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken);
}
