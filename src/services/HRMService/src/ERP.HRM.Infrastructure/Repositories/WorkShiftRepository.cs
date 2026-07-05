using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.HRM.Infrastructure.Repositories;

public class WorkShiftRepository : IWorkShiftRepository
{
    private readonly HrmDbContext _db;

    public WorkShiftRepository(HrmDbContext db) => _db = db;

    public async Task<WorkShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.WorkShifts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WorkShift>> GetAllAsync(bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var query = _db.WorkShifts.AsQueryable();

        if (activeOnly.HasValue)
            query = query.Where(s => s.IsActive == activeOnly.Value);

        return await query.OrderBy(s => s.StartTime).ToListAsync(cancellationToken);
    }

    public void Add(WorkShift shift) => _db.WorkShifts.Add(shift);

    public void Update(WorkShift shift) => _db.WorkShifts.Update(shift);
}
