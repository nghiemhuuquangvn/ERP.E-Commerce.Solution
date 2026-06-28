using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.CRM.Infrastructure.Repositories;

public class LeadRepository : ILeadRepository
{
    private readonly CrmDbContext _db;

    public LeadRepository(CrmDbContext db)
    {
        _db = db;
    }

    public async Task<Lead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Leads
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        await _db.Leads.AddAsync(lead, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _db.Leads.Update(lead);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<Lead>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Leads
            .Where(l => !l.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<Lead> Items, int TotalCount)> GetPagedAsync(
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Leads.Where(l => !l.IsDeleted);

        if (status.HasValue)
            query = query.Where(l => l.LeadStatus == status.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
