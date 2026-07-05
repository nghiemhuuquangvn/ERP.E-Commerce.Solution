using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.HRM.Infrastructure.Repositories;

public class EmployeeSnapshotRepository : IEmployeeSnapshotRepository
{
    private readonly HrmDbContext _context;

    public EmployeeSnapshotRepository(HrmDbContext context) => _context = context;

    public async Task<EmployeeSnapshot?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => await _context.EmployeeSnapshots
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

    public async Task<IReadOnlyList<EmployeeSnapshot>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await _context.EmployeeSnapshots
            .Where(e => e.EmploymentStatus == 0)
            .OrderBy(e => e.FullName)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<EmployeeSnapshot> Items, int TotalCount)> GetPagedAsync(
        string? search,
        Guid? departmentId,
        int? employmentStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeSnapshots.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(e =>
                e.FullName.ToLower().Contains(term) ||
                e.EmployeeNumber.ToLower().Contains(term) ||
                (e.Email != null && e.Email.ToLower().Contains(term)));
        }

        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId.Value);

        if (employmentStatus.HasValue)
            query = query.Where(e => e.EmploymentStatus == employmentStatus.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Upsert(EmployeeSnapshot snapshot)
    {
        var existing = _context.EmployeeSnapshots.Local.FirstOrDefault(e => e.Id == snapshot.Id);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(snapshot);
            return;
        }

        if (_context.EmployeeSnapshots.Any(e => e.Id == snapshot.Id))
            _context.EmployeeSnapshots.Update(snapshot);
        else
            _context.EmployeeSnapshots.Add(snapshot);
    }
}
