using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using ERP.HRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.HRM.Infrastructure.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly HrmDbContext _db;

    public AttendanceRepository(HrmDbContext db) => _db = db;

    public async Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<AttendanceRecord?> GetByEmployeeAndDateAsync(
        Guid employeeId, DateTime date, CancellationToken cancellationToken = default)
        => await _db.AttendanceRecords
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == date.Date, cancellationToken);

    public async Task<(IReadOnlyList<AttendanceRecord> Items, int TotalCount)> ListAsync(
        Guid? employeeId,
        DateTime? fromDate,
        DateTime? toDate,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AttendanceRecords.AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(r => r.EmployeeId == employeeId.Value);

        if (fromDate.HasValue)
            query = query.Where(r => r.Date >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(r => r.Date <= toDate.Value.Date);

        if (status.HasValue)
            query = query.Where(r => r.Status == (AttendanceStatus)status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<decimal> GetMonthlyOvertimeHoursAsync(
        Guid employeeId, int year, int month, CancellationToken cancellationToken = default)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        return await _db.AttendanceRecords
            .Where(r => r.EmployeeId == employeeId && r.Date >= startDate && r.Date <= endDate)
            .SumAsync(r => r.OvertimeHours, cancellationToken);
    }

    public async Task<(int Records, int AbsentDays, int HalfDays)> GetMonthlyCountsAsync(
        Guid employeeId, int year, int month, CancellationToken cancellationToken = default)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var rows = await _db.AttendanceRecords
            .Where(r => r.EmployeeId == employeeId && r.Date >= startDate && r.Date <= endDate)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return (rows.Sum(r => r.Count),
                rows.Where(r => r.Status == AttendanceStatus.Absent).Sum(r => r.Count),
                rows.Where(r => r.Status == AttendanceStatus.HalfDay).Sum(r => r.Count));
    }

    public void Add(AttendanceRecord record) => _db.AttendanceRecords.Add(record);

    public void Update(AttendanceRecord record) => _db.AttendanceRecords.Update(record);
}
