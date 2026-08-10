using ERP.CHB.Domain.Entities;

namespace ERP.CHB.Application.Interfaces;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Conversation>> GetByOwnerAsync(Guid tenantId, string? userId, string? guestSessionId, CancellationToken ct);
    Task AddAsync(Conversation conversation, CancellationToken ct);
    Task UpdateAsync(Conversation conversation, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
