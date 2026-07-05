using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.HRM.Infrastructure.Repositories;

public class PaySlipRepository : IPaySlipRepository
{
    private readonly HrmDbContext _context;

    public PaySlipRepository(HrmDbContext context)
    {
        _context = context;
    }

    public async Task<PaySlip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PaySlips
            .FirstOrDefaultAsync(ps => ps.Id == id && !ps.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<PaySlip> Items, int TotalCount)> ListAsync(
        Guid? employeeId,
        int? year,
        int? month,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PaySlips
            .Where(ps => !ps.IsDeleted)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(ps => ps.EmployeeId == employeeId.Value);

        if (year.HasValue)
            query = query.Where(ps => ps.Year == year.Value);

        if (month.HasValue)
            query = query.Where(ps => ps.Month == month.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(ps => ps.Year)
            .ThenByDescending(ps => ps.Month)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsForPeriodAsync(
        Guid employeeId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        return await _context.PaySlips
            .AnyAsync(ps => !ps.IsDeleted &&
                            ps.EmployeeId == employeeId &&
                            ps.Year == year &&
                            ps.Month == month,
                      cancellationToken);
    }

    public void Add(PaySlip paySlip)
    {
        _context.PaySlips.Add(paySlip);
    }
}
