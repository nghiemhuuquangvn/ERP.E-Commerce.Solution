using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Interfaces;

public interface IBarcodeMappingRepository
{
    Task<BarcodeMapping?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken);
    Task<List<BarcodeMapping>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task AddAsync(BarcodeMapping mapping, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
