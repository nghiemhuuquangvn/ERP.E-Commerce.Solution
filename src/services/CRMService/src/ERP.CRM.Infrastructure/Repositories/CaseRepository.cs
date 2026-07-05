using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.CRM.Infrastructure.Repositories;

public class CaseRepository : ICaseRepository
{
    private readonly CrmDbContext _db;

    public CaseRepository(CrmDbContext db)
    {
        _db = db;
    }

    public async Task<Case?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Cases
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(Case caseEntity, CancellationToken cancellationToken = default)
    {
        await _db.Cases.AddAsync(caseEntity, cancellationToken);
    }

    public Task UpdateAsync(Case caseEntity, CancellationToken cancellationToken = default)
    {
        _db.Cases.Update(caseEntity);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<Case>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Cases
            .Where(c => !c.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<Case> Items, int TotalCount)> GetPagedAsync(
        Guid? contactId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Cases.Where(c => !c.IsDeleted);

        if (contactId.HasValue)
            query = query.Where(c => c.CustomerId == contactId.Value);

        if (status.HasValue)
            query = query.Where(c => c.CaseStatus == status.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
