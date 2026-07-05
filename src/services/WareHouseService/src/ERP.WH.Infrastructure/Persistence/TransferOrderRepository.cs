using ERP.WH.Domain.Aggregates.Transfers;
using ERP.WH.Application.Commands.Transfers;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class TransferOrderRepository : ITransferOrderRepository
{
    private readonly WarehouseDbContext _context;

    public TransferOrderRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<TransferOrder?> GetByIdAsync(Guid transferOrderId, CancellationToken cancellationToken)
    {
        return await _context.TransferOrders.FirstOrDefaultAsync(t => t.TransferOrderId == transferOrderId, cancellationToken);
    }

    public async Task<List<TransferOrder>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.TransferOrders.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TransferOrder transferOrder, CancellationToken cancellationToken)
    {
        await _context.TransferOrders.AddAsync(transferOrder, cancellationToken);
    }

    public async Task UpdateAsync(TransferOrder transferOrder, CancellationToken cancellationToken)
    {
        _context.TransferOrders.Update(transferOrder);
    }
}
