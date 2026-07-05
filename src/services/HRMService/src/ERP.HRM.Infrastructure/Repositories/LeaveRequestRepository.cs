using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.HRM.Infrastructure.Repositories;

public class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly HrmDbContext _context;

    public LeaveRequestRepository(HrmDbContext context)
    {
        _context = context;
    }

    public async Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveRequests
            .FirstOrDefaultAsync(lr => lr.Id == id && !lr.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<LeaveRequest> Items, int TotalCount)> ListAsync(
        Guid? employeeId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveRequests
            .Where(lr => !lr.IsDeleted)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(lr => lr.EmployeeId == employeeId.Value);

        if (status.HasValue)
            query = query.Where(lr => lr.LeaveStatus == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(lr => lr.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(LeaveRequest leaveRequest)
    {
        _context.LeaveRequests.Add(leaveRequest);
    }

    public void Update(LeaveRequest leaveRequest)
    {
        _context.LeaveRequests.Update(leaveRequest);
    }
}
