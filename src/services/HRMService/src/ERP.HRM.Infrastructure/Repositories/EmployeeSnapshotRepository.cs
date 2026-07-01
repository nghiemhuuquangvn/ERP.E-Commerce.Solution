using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.HRM.Infrastructure.Repositories;

public class EmployeeSnapshotRepository : IEmployeeSnapshotRepository
{
    private readonly HrmDbContext _context;

    public EmployeeSnapshotRepository(HrmDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeSnapshot?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeSnapshots
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeSnapshot>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeSnapshots
            .Where(e => e.EmploymentStatus == 0)
            .OrderBy(e => e.FullName)
            .ToListAsync(cancellationToken);
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
        {
            _context.EmployeeSnapshots.Update(snapshot);
        }
        else
        {
            _context.EmployeeSnapshots.Add(snapshot);
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
