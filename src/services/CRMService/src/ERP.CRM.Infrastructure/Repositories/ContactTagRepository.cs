using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.CRM.Infrastructure.Repositories;

public class ContactTagRepository : IContactTagRepository
{
    private readonly CrmDbContext _db;

    public ContactTagRepository(CrmDbContext db) => _db = db;

    public async Task<IEnumerable<ContactTag>> GetByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default)
        => await _db.ContactTags
            .Where(t => t.ContactId == contactId)
            .OrderBy(t => t.Tag)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<Guid>> GetContactIdsByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var normalized = tag.Trim().ToLowerInvariant();
        return await _db.ContactTags
            .Where(t => t.Tag == normalized)
            .Select(t => t.ContactId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ContactTag tag, CancellationToken cancellationToken = default)
    {
        await _db.ContactTags.AddAsync(tag, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid contactId, string tag, CancellationToken cancellationToken = default)
    {
        var normalized = tag.Trim().ToLowerInvariant();
        var entity = await _db.ContactTags
            .FirstOrDefaultAsync(t => t.ContactId == contactId && t.Tag == normalized, cancellationToken);

        if (entity is not null)
        {
            _db.ContactTags.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default)
        => await _db.ContactTags
            .Select(t => t.Tag)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);
}
