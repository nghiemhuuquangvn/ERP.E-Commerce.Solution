using ERP.WH.Application.EventHandlers;

namespace ERP.WH.Infrastructure.Persistence;

public class ReceiptRepository : IReceiptRepository
{
    private readonly WarehouseDbContext _context;

    public ReceiptRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ReceiptSchedule receipt, CancellationToken cancellationToken)
    {
        // For now, we'll just log that a receipt is expected
        // In a real implementation, this would be stored in a separate table
        await Task.CompletedTask;
    }
}
