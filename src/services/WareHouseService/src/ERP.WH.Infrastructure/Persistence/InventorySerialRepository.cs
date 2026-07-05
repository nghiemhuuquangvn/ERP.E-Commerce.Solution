using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Application.Commands.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class InventorySerialRepository : IInventorySerialRepository
{
    private readonly WarehouseDbContext _context;

    public InventorySerialRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<InventorySerial?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken)
    {
        return await _context.InventorySerials.FirstOrDefaultAsync(s => s.SerialNumber == serialNumber, cancellationToken);
    }

    public async Task<List<InventorySerial>> GetByProductIdAsync(Guid productId, SerialStatus? status, CancellationToken cancellationToken)
    {
        var query = _context.InventorySerials.Where(s => s.ProductId == productId);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(InventorySerial serial, CancellationToken cancellationToken)
    {
        await _context.InventorySerials.AddAsync(serial, cancellationToken);
    }

    public async Task UpdateAsync(InventorySerial serial, CancellationToken cancellationToken)
    {
        _context.InventorySerials.Update(serial);
    }
}
