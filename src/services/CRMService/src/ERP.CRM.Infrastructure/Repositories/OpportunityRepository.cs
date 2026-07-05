using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Domain.Enums;
using ERP.CRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.CRM.Infrastructure.Repositories;

public class OpportunityRepository : IOpportunityRepository
{
    private readonly CrmDbContext _db;

    public OpportunityRepository(CrmDbContext db)
    {
        _db = db;
    }

    public async Task<Opportunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Opportunities
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        await _db.Opportunities.AddAsync(opportunity, cancellationToken);
    }

    public Task UpdateAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        _db.Opportunities.Update(opportunity);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<Opportunity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Opportunities
            .Where(o => !o.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<Opportunity> Items, int TotalCount)> GetPagedAsync(
        Guid? contactId,
        int? stage,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Opportunities.Where(o => !o.IsDeleted);

        if (contactId.HasValue)
            query = query.Where(o => o.ContactId == contactId.Value);

        if (stage.HasValue)
            query = query.Where(o => o.OpportunityStage == stage.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<Dictionary<int, int>> GetStageCounts(CancellationToken cancellationToken = default)
    {
        return await _db.Opportunities
            .Where(o => !o.IsDeleted)
            .GroupBy(o => o.OpportunityStage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Stage, x => x.Count, cancellationToken);
    }

    public async Task<decimal> GetTotalValue(CancellationToken cancellationToken = default)
    {
        return await _db.Opportunities
            .Where(o => !o.IsDeleted && o.EstimatedValue.HasValue)
            .SumAsync(o => o.EstimatedValue!.Value, cancellationToken);
    }

    public async Task<decimal> GetWonValue(CancellationToken cancellationToken = default)
    {
        return await _db.Opportunities
            .Where(o => !o.IsDeleted && o.OpportunityStage == (int)OpportunityStage.Won && o.EstimatedValue.HasValue)
            .SumAsync(o => o.EstimatedValue!.Value, cancellationToken);
    }
}
