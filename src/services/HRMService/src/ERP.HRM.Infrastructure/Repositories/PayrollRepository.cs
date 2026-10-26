using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.HRM.Infrastructure.Repositories;

public class PayrollRepository : IPayrollRepository
{
    private readonly HrmDbContext _db;

    public PayrollRepository(HrmDbContext db) => _db = db;

    public Task<EmployeeCompensation?> GetCompensationAsync(Guid employeeId, DateTime asOf, CancellationToken ct = default) =>
        _db.EmployeeCompensations
            .Where(c => !c.IsDeleted && c.EmployeeId == employeeId && c.EffectiveFrom <= asOf)
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    public Task<EmployeeCompensation?> GetCompensationOnAsync(Guid employeeId, DateTime effectiveFrom, CancellationToken ct = default) =>
        _db.EmployeeCompensations
            .FirstOrDefaultAsync(c => !c.IsDeleted && c.EmployeeId == employeeId && c.EffectiveFrom == effectiveFrom, ct);

    public async Task<IReadOnlyList<EmployeeCompensation>> ListCompensationsAsync(Guid employeeId, CancellationToken ct = default) =>
        await _db.EmployeeCompensations
            .Where(c => !c.IsDeleted && c.EmployeeId == employeeId)
            .OrderByDescending(c => c.EffectiveFrom)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, EmployeeCompensation>> GetCurrentCompensationsAsync(
        IReadOnlyCollection<Guid> employeeIds, DateTime asOf, CancellationToken ct = default)
    {
        var rows = await _db.EmployeeCompensations
            .Where(c => !c.IsDeleted && employeeIds.Contains(c.EmployeeId) && c.EffectiveFrom <= asOf)
            .ToListAsync(ct);
        return rows.GroupBy(c => c.EmployeeId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EffectiveFrom).First());
    }

    public void Add(EmployeeCompensation compensation) => _db.EmployeeCompensations.Add(compensation);

    public Task<Dependent?> GetDependentAsync(Guid id, CancellationToken ct = default) =>
        _db.Dependents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

    public async Task<IReadOnlyList<Dependent>> ListDependentsAsync(Guid employeeId, CancellationToken ct = default) =>
        await _db.Dependents
            .Where(d => !d.IsDeleted && d.EmployeeId == employeeId)
            .OrderBy(d => d.FromMonth).ThenBy(d => d.FullName)
            .ToListAsync(ct);

    public void Add(Dependent dependent) => _db.Dependents.Add(dependent);

    public Task<PayrollRateSet?> GetRateSetAsync(DateTime asOf, CancellationToken ct = default) =>
        _db.PayrollRateSets.Include(r => r.Brackets)
            .Where(r => !r.IsDeleted && r.EffectiveFrom <= asOf)
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    public Task<PayrollRateSet?> GetRateSetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.PayrollRateSets.Include(r => r.Brackets).FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    public async Task<IReadOnlyList<PayrollRateSet>> ListRateSetsAsync(CancellationToken ct = default) =>
        await _db.PayrollRateSets.Include(r => r.Brackets)
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.EffectiveFrom)
            .ToListAsync(ct);

    public Task<bool> AnyRateSetAsync(CancellationToken ct = default) => _db.PayrollRateSets.AnyAsync(r => !r.IsDeleted, ct);

    public void Add(PayrollRateSet rateSet) => _db.PayrollRateSets.Add(rateSet);
    public void Add(PitBracket bracket) => _db.PitBrackets.Add(bracket);
    public void Remove(PitBracket bracket) => _db.PitBrackets.Remove(bracket);

    public Task<LeaveBalance?> GetLeaveBalanceAsync(Guid employeeId, int year, int leaveType, CancellationToken ct = default) =>
        _db.LeaveBalances.FirstOrDefaultAsync(b => !b.IsDeleted && b.EmployeeId == employeeId && b.Year == year && b.LeaveType == leaveType, ct);

    public async Task<IReadOnlyList<LeaveBalance>> ListLeaveBalancesAsync(Guid? employeeId, int year, CancellationToken ct = default) =>
        await _db.LeaveBalances
            .Where(b => !b.IsDeleted && b.Year == year && (employeeId == null || b.EmployeeId == employeeId))
            .OrderBy(b => b.EmployeeId).ThenBy(b => b.LeaveType)
            .ToListAsync(ct);

    public void Add(LeaveBalance balance) => _db.LeaveBalances.Add(balance);
}
