using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class BarcodeMappingRepository : IBarcodeMappingRepository
{
    private readonly WarehouseDbContext _context;

    public BarcodeMappingRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<BarcodeMapping?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken)
        => await _context.BarcodeMappings.AsNoTracking().FirstOrDefaultAsync(b => b.Barcode == barcode, cancellationToken);

    public async Task<List<BarcodeMapping>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken)
        => await _context.BarcodeMappings.AsNoTracking().Where(b => b.ProductId == productId).ToListAsync(cancellationToken);

    public async Task AddAsync(BarcodeMapping mapping, CancellationToken cancellationToken)
        => await _context.BarcodeMappings.AddAsync(mapping, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.BarcodeMappings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity != null)
            _context.BarcodeMappings.Remove(entity);
    }
}
