using ERP.CHB.Application.Interfaces;
using ERP.CHB.Domain.Entities;
using ERP.CHB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.CHB.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly ChatBotDbContext _db;

    public ConversationRepository(ChatBotDbContext db)
    {
        _db = db;
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Conversations.Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Conversation>> GetByOwnerAsync(Guid tenantId, string? userId, string? guestSessionId, CancellationToken ct)
    {
        var query = _db.Conversations.Where(c => c.TenantId == tenantId);
        query = userId is not null
            ? query.Where(c => c.CreatedBy == userId)
            : query.Where(c => c.GuestSessionId == guestSessionId);

        return await query.OrderByDescending(c => c.StartedAt).ToListAsync(ct);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken ct) =>
        await _db.Conversations.AddAsync(conversation, ct);

    public Task UpdateAsync(Conversation conversation, CancellationToken ct)
    {
        _db.Conversations.Update(conversation);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
