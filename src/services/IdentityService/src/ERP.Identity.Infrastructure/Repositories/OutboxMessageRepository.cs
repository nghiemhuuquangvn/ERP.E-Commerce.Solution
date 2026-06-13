using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories;

public class OutboxMessageRepository : GenericRepository<OutboxMessage>, IOutboxMessageRepository
{
    public OutboxMessageRepository(AuthDbContext dbContext) : base(dbContext)
    {
    }

    public new async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await base.AddAsync(message, cancellationToken);
    }

    public Task<List<OutboxMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var pendingMessages = _dbContext.OutboxMessages.Where(m => m.ProcessedOnUtc == null)
                                                    .OrderBy(m => m.OccurredOnUtc)
                                                    .Take(20)
                                                    .ToListAsync(cancellationToken);
        return pendingMessages;
    }
}