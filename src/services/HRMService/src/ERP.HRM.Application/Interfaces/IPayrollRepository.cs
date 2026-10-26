using ERP.HRM.Domain.Entities;

namespace ERP.HRM.Application.Interfaces;

/// <summary>Salary terms, dependents, the date-effective rate sets and leave balances (ROADMAP P1-7).</summary>
public interface IPayrollRepository
{
    // Salary terms: the row with the latest EffectiveFrom on or before the date applies.
    Task<EmployeeCompensation?> GetCompensationAsync(Guid employeeId, DateTime asOf, CancellationToken ct = default);
    Task<EmployeeCompensation?> GetCompensationOnAsync(Guid employeeId, DateTime effectiveFrom, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeCompensation>> ListCompensationsAsync(Guid employeeId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, EmployeeCompensation>> GetCurrentCompensationsAsync(IReadOnlyCollection<Guid> employeeIds, DateTime asOf, CancellationToken ct = default);
    void Add(EmployeeCompensation compensation);

    Task<Dependent?> GetDependentAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Dependent>> ListDependentsAsync(Guid employeeId, CancellationToken ct = default);
    void Add(Dependent dependent);

    // Rate sets: the one with the latest EffectiveFrom on or before the date applies.
    Task<PayrollRateSet?> GetRateSetAsync(DateTime asOf, CancellationToken ct = default);
    Task<PayrollRateSet?> GetRateSetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollRateSet>> ListRateSetsAsync(CancellationToken ct = default);
    Task<bool> AnyRateSetAsync(CancellationToken ct = default);
    void Add(PayrollRateSet rateSet);
    void Add(PitBracket bracket);
    void Remove(PitBracket bracket);

    Task<LeaveBalance?> GetLeaveBalanceAsync(Guid employeeId, int year, int leaveType, CancellationToken ct = default);
    Task<IReadOnlyList<LeaveBalance>> ListLeaveBalancesAsync(Guid? employeeId, int year, CancellationToken ct = default);
    void Add(LeaveBalance balance);
}
