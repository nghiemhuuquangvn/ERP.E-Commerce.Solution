using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly MasterDataDbContext _context;

    public TeamRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Team> Items, int TotalCount)> ListAsync(
        Guid? departmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Teams
            .Where(t => !t.IsDeleted)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(t => t.DepartmentId == departmentId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .AnyAsync(t => !t.IsDeleted && t.Code == code, cancellationToken);
    }

    public void Add(Team team)
    {
        _context.Teams.Add(team);
    }

    public void Update(Team team)
    {
        _context.Teams.Update(team);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
