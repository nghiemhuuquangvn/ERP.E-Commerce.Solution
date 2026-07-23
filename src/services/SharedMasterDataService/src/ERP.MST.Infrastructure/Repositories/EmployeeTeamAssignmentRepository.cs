using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class EmployeeTeamAssignmentRepository : IEmployeeTeamAssignmentRepository
{
    private readonly MasterDataDbContext _context;

    public EmployeeTeamAssignmentRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeTeamAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeTeamAssignments
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<EmployeeTeamAssignment> Items, int TotalCount)> ListAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeTeamAssignments
            .Where(a => !a.IsDeleted && a.EmployeeId == employeeId)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.EffectiveFrom)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(EmployeeTeamAssignment assignment)
    {
        _context.EmployeeTeamAssignments.Add(assignment);
    }

    public void Update(EmployeeTeamAssignment assignment)
    {
        _context.EmployeeTeamAssignments.Update(assignment);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
