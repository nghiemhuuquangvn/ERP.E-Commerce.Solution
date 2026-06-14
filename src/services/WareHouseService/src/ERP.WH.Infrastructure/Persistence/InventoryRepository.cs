using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Application.Commands.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class InventoryRepository : IInventoryRepository
{
    private readonly WarehouseDbContext _context;

    public InventoryRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<Inventory> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId, cancellationToken);
    }

    public async Task UpdateAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        _context.Inventories.Update(inventory);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        await _context.Inventories.AddAsync(inventory, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Inventory>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Inventories.ToListAsync(cancellationToken);
    }
}
